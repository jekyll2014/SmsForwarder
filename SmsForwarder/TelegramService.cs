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
        private readonly CancellationTokenSource? _cts;
        private readonly TelegramBotClient? _botClient;
        private readonly List<long> _authorisedUsers;

        private const string StatusCommand = "/status";
        private const string StatusCommandDescription = "Get status";
        private const string SmsSendCommand = "/smssend";
        private const string SmsSendCommandDescription = "Send SMS";
        private const string SmsListCommand = "/smslist";
        private const string SmsListCommandDescription = "Get recent SMS list";
        private const string CallListCommand = "/phonelog";
        private const string CallListommandDescription = "Get call-log";

        private bool _disposedValue;

        public TelegramService(string token, IEnumerable<long> authorisedUsers)
        {
            _authorisedUsers = authorisedUsers.ToList();

            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Telegram service not setup.");

                return;
            }

            Console.WriteLine("Starting Telefram service...");
            _cts = new CancellationTokenSource();
            _botClient = new TelegramBotClient(token);
            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            var receiverOptions = new ReceiverOptions()
            {
                AllowedUpdates = new UpdateType[] { UpdateType.Message, UpdateType.CallbackQuery }
            };

            try
            {
                if (!_botClient.TestApiAsync().Result)
                {
                    Console.WriteLine($"Telegram connection failed.");
                    _botClient = null;

                    return;
                }

                _botClient.SetMyCommandsAsync(new[]
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
                    /*new BotCommand()
                    {
                        Command = SmsGetCommand.TrimStart('/'),
                        Description = SmsGetCommandDescription
                    }*/
                }).Wait();

                _botClient.StartReceiving(
                    updateHandler: HandleUpdateAsync,
                    pollingErrorHandler: HandlePollingErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: _cts.Token
                );

                var me = _botClient.GetMeAsync().Result;
                Console.WriteLine($"...listening for @{me.Username} [{me.Id}]");
                SendText(_authorisedUsers, $"...listening for @{me.Username} [{me.Id}]");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"...connection failed: {ex}");
                if (ex is ApiRequestException apiEx && apiEx.ErrorCode == 401)
                {
                    Console.WriteLine("Check your \"Telegram\": { \"Token\" } .");
                }
            }
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

            if (!_authorisedUsers.Contains(senderId))
            {
                SendText(senderId, "You are not authorized to send messages", _cts?.Token ?? CancellationToken.None)
                    .Wait(_cts?.Token ?? CancellationToken.None);
            }

            Console.WriteLine($"Received a '{messageText}' message from \"@{senderName}\"[{senderId}].");

            await Task.Run(async () =>
            {
                //return status of the program
                if (messageText.StartsWith(StatusCommand, StringComparison.OrdinalIgnoreCase))
                {
                    var smsAllowed = MainActivity.CheckSmsPermission().Result;
                    var phoneAllowed = MainActivity.CheckPhonePermission().Result;
                    // battery status
                    // GSM network status
                    // 
                    await SendText(chatId, $"SMS allowed: {smsAllowed}\r\nPhone allowed: {phoneAllowed}", CancellationToken.None);
                }
                //send SMS message
                else if (messageText.StartsWith(SmsSendCommand, StringComparison.OrdinalIgnoreCase))
                {
                    messageText = messageText.Substring(SmsSendCommand.Length + 1);
                    var address = messageText.Split(new[] { ' ', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault() ?? string.Empty;

                    if (MainActivity.SendSms(address, messageText.Substring(address.Length + 1)))
                    {
                        SendText(senderId, $"SMS sent to {address}", _cts?.Token ?? CancellationToken.None)
                            .Wait(_cts?.Token ?? CancellationToken.None);
                    }
                    else
                    {
                        SendText(senderId, $"SMS not sent to {address}", _cts?.Token ?? CancellationToken.None)
                            .Wait(_cts?.Token ?? CancellationToken.None);
                    }
                }
                //get list of SMS
                else if (messageText.StartsWith(SmsListCommand, StringComparison.OrdinalIgnoreCase))
                {
                    var smsList = GetAllSms().OrderBy(n => n.Id).TakeLast(10);
                    var sb = new StringBuilder();
                    foreach (var sms in smsList)
                    {
                        sb.AppendLine(sms.ToString());
                    }

                    await SendText(chatId, sb.ToString(), CancellationToken.None);
                }
                //get recent calls
                else if (messageText.StartsWith(CallListCommand, StringComparison.OrdinalIgnoreCase))
                { }
            });
        }

        private async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken token)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error: [{apiRequestException.ErrorCode}] {apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(errorMessage);
        }

        public async Task<bool> SendText(IEnumerable<long> userIds, string text)
        {
            var result = true;
            foreach (var user in userIds)
            {
                if (await SendText(user, text, CancellationToken.None) == null)
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

            Console.WriteLine($"Sending text to [{chatId}]: \"{text}\"");

            try
            {
                return await _botClient.SendTextMessageAsync(chatId, text, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Telegram exception: {ex}");
                return null;
            }
        }

        public static IEnumerable<StoredSmsContent> GetAllSms()
        {
            const string inbox = "content://sms/inbox";
            var reqCols = new string[] { "_id", "thread_id", "address", "person", "date", "body", "type" };
            var uri = Android.Net.Uri.Parse(inbox);
            var cursor = Android.App.Application.Context.ContentResolver?.Query(uri, reqCols, null, null, null);
            var items = new List<StoredSmsContent>();
            if (cursor?.MoveToFirst() ?? false)
            {
                do
                {
                    var messageId = cursor.GetString(cursor.GetColumnIndex(reqCols[0]));
                    var threadId = cursor.GetString(cursor.GetColumnIndex(reqCols[1]));
                    var address = cursor.GetString(cursor.GetColumnIndex(reqCols[2]));
                    var name = cursor.GetString(cursor.GetColumnIndex(reqCols[3]));
                    var date = cursor.GetString(cursor.GetColumnIndex(reqCols[4]));
                    var msg = cursor.GetString(cursor.GetColumnIndex(reqCols[5]));
                    var type = cursor.GetString(cursor.GetColumnIndex(reqCols[6]));

                    var dt = DateTime.UnixEpoch.AddMilliseconds(long.Parse(date ?? "0"));
                    var item = new StoredSmsContent()
                    {
                        Id = long.Parse(messageId ?? "0"),
                        ThreadId = long.Parse(threadId ?? "0"),
                        Address = address ?? "",
                        Person = name ?? "",
                        Date = dt,
                        Text = msg ?? "",
                        Type = type == "1" ? "Received" : $"[{type}] Sent/Draft/..."
                    };

                    items.Add(item);
                } while (cursor.MoveToNext());
            }

            return items;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _botClient?.CloseAsync();
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
