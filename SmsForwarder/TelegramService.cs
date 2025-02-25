﻿using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Android.Telephony;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using Message = Telegram.Bot.Types.Message;

namespace SmsForwarder
{
    public class TelegramService : IDisposable
    {
        private CancellationTokenSource? _cts;
        private TelegramBotClient? _botClient;

        private string _token;

        public long[] AuthorisedUsers;

        private const string StatusCommand = "/status";
        private const string StatusCommandDescription = "Get status";
        private const string SmsSendCommand = "/smssend";
        private const string SmsSendCommandDescription = "Send SMS";
        private const string SmsListCommand = "/smslist";
        private const string SmsListCommandDescription = "Get recent SMS list";
        private const string CallListCommand = "/phonelog";
        private const string CallListommandDescription = "Get call-log";

        private char[] _messageBounds = new char[] { '<', '>' };
        private bool _sendingSms = false;

        private bool _disposedValue;

        private SmsManager? _smsManager;

        public TelegramService(string token, long[] authorisedUsers, SmsManager? smsManager)
        {
            _token = token;
            AuthorisedUsers = authorisedUsers;

            if (!string.IsNullOrEmpty(_token))
            {
                MainActivity.ShowToast($"Starting Telegram service...");
                Connect();
            }

            _smsManager = smsManager;
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken token)
        {
            string messageText;
            long senderId;
            string senderName;
            ChatId chatId;
            if (update.Message is { } message)
            {
                messageText = update.Message.Text ?? string.Empty;
                senderId = message.From?.Id ?? -1;
                senderName = message.From?.Username ?? string.Empty;
                chatId = message.Chat.Id;
            }
            else if (update.CallbackQuery is { } query)
            {
                messageText = query.Data ?? string.Empty;
                senderId = query.From.Id;
                senderName = query.From.Username ?? string.Empty;
                chatId = query.Message?.Chat.Id ?? -1;
            }
            else
            {
                return;
            }

            if (!AuthorisedUsers.Contains(senderId))
            {
                await SendText(senderId,
                    "You are not authorized to send messages",
                    _cts?.Token ?? CancellationToken.None);
            }

            MainActivity.ShowToast($"Received a '{messageText}' message from \"@{senderName}\"[{senderId}]");

            if (_sendingSms)
            {
                messageText = $"{SmsSendCommand} {messageText}";
                _sendingSms = false;
            }

            await Task.Run(async () =>
            {
                try
                {
                    //return status of the program
                    if (messageText.StartsWith(StatusCommand, StringComparison.OrdinalIgnoreCase))
                    {
                        //var smsAllowed = MainActivity.CheckSmsPermission();
                        //var phoneAllowed = MainActivity.CheckPhonePermission();
                        var gsmSignal = GetCellphoneProvider();
                        var batteryLevel = GetBatteryLevel();

                        await SendText(chatId,
                            //$"SMS allowed: {smsAllowed}\r\n" +
                            //$"Phone allowed: {phoneAllowed}\r\n" +
                            $"GSM provider: {gsmSignal.CarrierName}\r\n" +
                            $"GSM signal: {gsmSignal.SignalStrength}\r\n" +
                            $"Battery: {batteryLevel}%",
                            _cts?.Token ?? CancellationToken.None);
                    }
                    //send SMS message
                    else if (messageText.StartsWith(SmsSendCommand, StringComparison.OrdinalIgnoreCase))
                    {
                        if (messageText.Length <= SmsSendCommand.Length + 4)
                        {
                            _sendingSms = true;
                            await SendText(senderId, $"Please send me SMS address and text like: <phone_number> <text of the message>", _cts?.Token ?? CancellationToken.None);

                            return;
                        }

                        messageText = messageText.Substring(SmsSendCommand.Length + 1);
                        var address = messageText.Split(new[] { ' ', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .FirstOrDefault() ?? string.Empty;
                        address = address.Trim(_messageBounds);
                        messageText = messageText.Substring(address.Length + 1);
                        messageText = messageText.Trim(_messageBounds);
                        if (SendSms(address, messageText))
                        {
                            await SendText(senderId, $"SMS sent to {address}", _cts?.Token ?? CancellationToken.None);
                        }
                        else
                        {
                            await SendText(senderId, $"SMS not sent to {address}", _cts?.Token ?? CancellationToken.None);
                        }
                    }
                    //get list of SMS
                    else if (messageText.StartsWith(SmsListCommand, StringComparison.OrdinalIgnoreCase))
                    {
                        var smsList = GetSmsInbox().OrderBy(n => n.Date).TakeLast(10);
                        var sb = new StringBuilder();
                        foreach (var sms in smsList)
                        {
                            sb.AppendLine(sms.ToString());
                        }

                        await SendText(chatId, sb.ToString(), _cts?.Token ?? CancellationToken.None);
                    }
                    //get recent calls
                    else if (messageText.StartsWith(CallListCommand, StringComparison.OrdinalIgnoreCase))
                    {
                        var callLog = GetCallLog().OrderBy(n => n.Date).TakeLast(10);
                        var sb = new StringBuilder();
                        foreach (var sms in callLog)
                        {
                            sb.AppendLine(sms.ToString());
                        }

                        await SendText(chatId, sb.ToString(), _cts?.Token ?? CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    MainActivity.ShowToast($"Exception processing command \"{messageText}\" from \"@{senderName}\"[{senderId}]: {ex}");
                }
            });
        }

        private async Task HandlePollingErrorAsync(
            ITelegramBotClient botClient,
            Exception exception,
            CancellationToken token)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error: [{apiRequestException.ErrorCode}] {apiRequestException.Message}",
                _ => exception.ToString()
            };

            MainActivity.ShowToast(errorMessage);
        }

        public async Task<bool> Connect()
        {
            _cts?.Dispose();

            if (_botClient != null)
            {
                Disconnect();
            }

            _cts = new CancellationTokenSource();

            _botClient = new TelegramBotClient(_token);
            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            var receiverOptions = new ReceiverOptions()
            {
                AllowedUpdates = new UpdateType[] { UpdateType.Message, UpdateType.CallbackQuery }
            };

            try
            {
                if (!(await _botClient.TestApi()))
                {
                    MainActivity.ShowToast($"Telegram connection failed");
                    _botClient = null;

                    return false;
                }

                await _botClient.SetMyCommands(new[]
                {
                    new BotCommand()
                    {
                        Command = StatusCommand.TrimStart('/'),
                        Description = StatusCommandDescription
                    },
                    new BotCommand()
                    {
                        Command = SmsSendCommand.TrimStart('/'),
                        Description = SmsSendCommandDescription
                    },
                    new BotCommand()
                    {
                        Command = SmsListCommand.TrimStart('/'),
                        Description = SmsListCommandDescription
                    },
                    new BotCommand()
                    {
                        Command = CallListCommand.TrimStart('/'),
                        Description = CallListommandDescription
                    }
                });

                _botClient.StartReceiving(
                    updateHandler: HandleUpdateAsync,
                    HandlePollingErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: _cts.Token);

                var me = await _botClient.GetMe();
                MainActivity.ShowToast($"...listening for @{me.Username} [{me.Id}]");
                await SendText(AuthorisedUsers,
                    $"...listening for @{me.Username} [{me.Id}]",
                    _cts.Token);
            }
            catch (Exception ex)
            {
                var message = $"...connection failed: {ex}\r\n";
                if (ex is ApiRequestException apiEx && apiEx.ErrorCode == 401)
                    message += $"Check your telegram token: {_token}";

                MainActivity.ShowToast(message);

                return false;
            }

            return true;
        }

        public void Disconnect()
        {
            _botClient?.Close();
            _botClient = null;
            _cts?.Cancel();
            _cts?.Dispose();
        }

        public async Task<bool> SetToken(string value)
        {
            if (value == _token)
                return true;

            if (string.IsNullOrEmpty(value))
                return false;

            _token = value;
            Disconnect();

            if (!string.IsNullOrEmpty(_token))
                return await Connect();

            return false;
        }

        public async Task<bool> SendText(IEnumerable<long> userIds, string text, CancellationToken cancellationToken)
        {
            var result = true;
            foreach (var user in userIds)
            {
                if (await SendText(user, text, cancellationToken) == null)
                    result = false;
            }

            return result;
        }

        public async Task<Message?> SendText(ChatId chatId,
            string text,
            CancellationToken cancellationToken)
        {
            if (_botClient == null)
                return null;

            MainActivity.ShowToast($"Sending text to [{chatId}]: \"{text}\"");

            try
            {
                return await _botClient.SendMessage(chatId, text, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                MainActivity.ShowToast($"Telegram send message exception: {ex.Message}");

                return null;
            }
        }

        private static GsmInfo GetCellphoneProvider()
        {
            var manager = Android.App.Application.Context.GetSystemService(Android.Content.Context.TelephonyService) as Android.Telephony.TelephonyManager;

            return new GsmInfo()
            {
                CarrierName = manager?.NetworkOperatorName ?? "",
                SignalStrength = manager?.SignalStrength?.Level ?? -1,
                NetworkType = manager?.DataNetworkType.ToString() ?? ""
            };
        }

        private static int GetBatteryLevel()
        {
            try
            {
                using (var filter = new IntentFilter(Intent.ActionBatteryChanged))
                {
                    using (var battery = Application.Context.RegisterReceiver(null, filter))
                    {
                        var level = battery?.GetIntExtra(BatteryManager.ExtraLevel, -1) ?? -1;
                        var scale = battery?.GetIntExtra(BatteryManager.ExtraScale, -1) ?? -1;

                        return (int)Math.Floor(level * 100D / scale);
                    }
                }
            }
            catch (Exception ex)
            {
                MainActivity.ShowToast("Unable to get battery level, ensure you have android.permission.BATTERY_STATS set in AndroidManifest.");
            }

            return -1;
        }

        public static IEnumerable<StoredSmsRecord> GetSmsInbox()
        {
            var uri = Android.Provider.Telephony.Sms.Inbox.ContentUri;
            var cursor = Android.App.Application.Context.ContentResolver?.Query(
                Telephony.Sms.Inbox.ContentUri,
                null,
                null,
                null,
                null);
            var items = new List<StoredSmsRecord>();
            if (cursor?.MoveToFirst() ?? false)
            {
                do
                {
                    var messageId = cursor.GetString(cursor.GetColumnIndex(Telephony.Sms.Inbox.InterfaceConsts.Id)) ?? "-1";
                    var threadId = cursor.GetString(cursor.GetColumnIndex(Telephony.Sms.Inbox.InterfaceConsts.ThreadId)) ?? "-1";
                    var address = cursor.GetString(cursor.GetColumnIndex(Telephony.Sms.Inbox.InterfaceConsts.Address)) ?? "";
                    var name = cursor.GetString(cursor.GetColumnIndex(Telephony.Sms.Inbox.InterfaceConsts.Person)) ?? "";
                    var date = cursor.GetString(cursor.GetColumnIndex(Telephony.Sms.Inbox.InterfaceConsts.Date)) ?? "0";
                    var msg = cursor.GetString(cursor.GetColumnIndex(Telephony.Sms.Inbox.InterfaceConsts.Body)) ?? "";
                    var type = cursor.GetString(cursor.GetColumnIndex(Telephony.Sms.Inbox.InterfaceConsts.Type)) ?? "-1";
                    var subject = cursor.GetString(cursor.GetColumnIndex(Telephony.Sms.Inbox.InterfaceConsts.Subject)) ?? "";

                    var dt = DateTime.UnixEpoch.AddMilliseconds(long.Parse(date));
                    var item = new StoredSmsRecord()
                    {
                        Id = long.Parse(messageId),
                        ThreadId = long.Parse(threadId),
                        Address = address,
                        Name = name,
                        Date = dt,
                        Subject = subject,
                        Text = msg,
                        Type = type == "1" ? "Received" : $"[{type}] Sent/Draft/..."
                    };

                    items.Add(item);
                } while (cursor.MoveToNext());
            }

            return items;
        }

        public static IEnumerable<CallLogRecord> GetCallLog()
        {
            // filter call logs by type = missed
            // string queryFilter = String.Format ("{0}={1}", CallLog.Calls.NetworkType, (int)CallType.Missed);

            // filter in desc order limit by 3
            // string querySorter = String.Format ("{0} desc limit 3", CallLog.Calls.Date);

            // filter in desc order limit by no
            //var querySorter = string.Format("{0} desc ", CallLog.Calls.Date);

            var queryData = Android.App.Application.Context.ContentResolver?.Query(
                CallLog.Calls.ContentUri,
                null,
                null,
                null,
                null);
            var items = new List<CallLogRecord>();

            if (queryData?.MoveToFirst() ?? false)
            {
                do
                {
                    var number = queryData.GetString(queryData.GetColumnIndex(CallLog.Calls.Number)) ?? "";
                    var name = queryData.GetString(queryData.GetColumnIndex(CallLog.Calls.CachedName)) ?? "";
                    var date = queryData.GetString(queryData.GetColumnIndex(CallLog.Calls.Date)) ?? "0";
                    //1-incoming; 2-outgoing; 3-missed---
                    var type = queryData.GetString(queryData.GetColumnIndex(CallLog.Calls.Type)) ?? "";
                    var duration = queryData.GetString(queryData.GetColumnIndex(CallLog.Calls.Duration)) ?? "";
                    var dt = DateTime.UnixEpoch.AddMilliseconds(long.Parse(date));
                    var item = new CallLogRecord()
                    {
                        Name = name,
                        Phone = number,
                        Date = dt,
                        Type = type == "1" ? "Incoming" : type == "3" ? "Missed" : "Outgoing",
                        Duration = duration
                    };

                    items.Add(item);
                } while (queryData.MoveToNext());
            }

            return items;
        }

        public bool SendSms(string address, string text)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(text))
                return false;

            try
            {
                _smsManager?.SendTextMessage(address, null, text, null, null);
                MainActivity.ShowToast($"SMS sent");
            }
            catch (Exception ex)
            {
                MainActivity.ShowToast($"Exception sending SMS: {ex.Message}");

                return false;
            }

            return true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _botClient?.Close();
                    _cts?.Cancel();
                    _cts?.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
