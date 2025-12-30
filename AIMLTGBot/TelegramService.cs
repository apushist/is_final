
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace AIMLTGBot
{
    public class TelegramService : IDisposable
    {
		private readonly MagicEye imageProcessor;
		private readonly StudentNetwork network = new StudentNetwork();
		private readonly TelegramBotClient client;
        private readonly AIMLService aiml;
        // CancellationToken - инструмент для отмены задач, запущенных в отдельном потоке
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        public string Username { get; }

        public TelegramService(string token, AIMLService aimlService)
        {
            aiml = aimlService;
			imageProcessor = new MagicEye(network);
			client = new TelegramBotClient(token);
            client.StartReceiving(HandleUpdateMessageAsync, HandleErrorAsync, new ReceiverOptions
            {   // Подписываемся только на сообщения
                AllowedUpdates = new[] { UpdateType.Message }
            },
            cancellationToken: cts.Token);
            // Пробуем получить логин бота - тестируем соединение и токен
            Username = client.GetMeAsync().Result.Username;
        }

        async Task HandleUpdateMessageAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var message = update.Message;
            var chatId = message.Chat.Id;
            var username = message.Chat.FirstName;
            if (message.Type == MessageType.Text)
            {
                var messageText = update.Message.Text;
                if(messageText.StartsWith("/"))
                    messageText = messageText.Substring(1);
                messageText = messageText.ToLower().Trim().Replace('ё','е');//меняю регистр исключительно из-за замены
                Console.WriteLine($"Received a '{messageText}' message in chat {chatId} with {username}.");
                string answer = aiml.Talk(chatId, username, messageText);
                if(answer != null && answer.Trim() != "")
				    // Echo received message text
				    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: answer,
                        cancellationToken: cancellationToken);
                return;
            }
            if (message.Type == MessageType.Photo)
            {
                var photoId = message.Photo.Last().FileId;
                Telegram.Bot.Types.File fl = await client.GetFileAsync(photoId, cancellationToken: cancellationToken);
                var imageStream = new MemoryStream();
                await client.DownloadFileAsync(fl.FilePath, imageStream, cancellationToken: cancellationToken);
                try
                {
					using (var bitmap = new Bitmap(imageStream))
					{
						imageProcessor.ProcessImage(bitmap);

						var result = imageProcessor.result;
						string answer = aiml.Talk(chatId, username, result.ToString());
						if (answer != null && answer.Trim() != "")
							// Echo received message text
							await botClient.SendTextMessageAsync(
								chatId: chatId,
								text: answer,
								cancellationToken: cancellationToken);
						//await botClient.SendTextMessageAsync(
						//	chatId: chatId,
						//	text: $"{result.ToString()}",
						//	cancellationToken: cancellationToken
						//);
					}
				}
                catch {
					await botClient.SendTextMessageAsync(
									chatId: chatId,
									text: "Произошла ошибка при обработке изображения",
									cancellationToken: cancellationToken);
				}
				return;
            }
            if (message.Type == MessageType.Video)
            {
                await client.SendTextMessageAsync(message.Chat.Id, aiml.Talk(chatId, username, "Видео"), cancellationToken: cancellationToken);
                return;
            }
            if (message.Type == MessageType.Audio)
            {
                await client.SendTextMessageAsync(message.Chat.Id, aiml.Talk(chatId, username, "Аудио"), cancellationToken: cancellationToken);
                return;
            }
        }

        Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var apiRequestException = exception as ApiRequestException;
            if (apiRequestException != null)
                Console.WriteLine($"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}");
            else
                Console.WriteLine(exception.ToString());
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            // Заканчиваем работу - корректно отменяем задачи в других потоках
            // Отменяем токен - завершатся все асинхронные таски
            cts.Cancel();
        }
    }
}
