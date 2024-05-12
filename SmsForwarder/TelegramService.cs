using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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
        private const string SmsListCommandDescription = "Get SMS list";
        private const string SmsGetCommand = "/smsget";
        private const string SmsGetCommandDescription = "Get SMS";

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
                    new BotCommand()
                    {
                        Command = SmsGetCommand.TrimStart('/'),
                        Description = SmsGetCommandDescription
                    }
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
                //get SMS
                //get recent calls
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