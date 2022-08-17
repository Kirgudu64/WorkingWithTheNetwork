using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.InputFiles;




string token = System.IO.File.ReadAllText(@"G:\Telegram_bot\token.txt");
string path = $@"G:\Telegram_bot\files\";
using var cts = new CancellationTokenSource();

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = { }
};

var botClient = new TelegramBotClient($"{token}");
botClient.StartReceiving(
    HandleUpdatesAsync,
    HandleErrorAsync,
    receiverOptions,
    cancellationToken: cts.Token);

var me = await botClient.GetMeAsync();
Console.WriteLine($"Бот начал работу @{me.Username}");
Console.ReadLine();
cts.Cancel();

#region Обработка обновлений

async Task HandleUpdatesAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{    
    if (update.Type == UpdateType.Message && update?.Message?.Text != null)
    {
        await HandleMessage(botClient, update.Message, $"{path}{update.Message.Chat.Username}");
        return;
    }

    if (update.Type == UpdateType.CallbackQuery)
    {        
        await HandleCallbackQuery(botClient, update.CallbackQuery);     
        return;
    }

    switch (update.Message.Type)
    {
        case MessageType.Document:
            var document = update.Message.Document;
            var fileId = document.FileId;

            await SaveFile(botClient, $"{path}{update.Message.Chat.Username}", fileId, document.FileName);

            await botClient.SendTextMessageAsync(update.Message.Chat.Id, text: $"Файл {document.FileName} успешно сохранен");
            break;

        case MessageType.Video:

            await SaveFile(botClient, $"{path}{update.Message.Chat.Username}", update.Message.Video.FileId, update.Message.Video.FileName);

            await botClient.SendTextMessageAsync(update.Message.Chat.Id, text: $"Видео {update.Message.Video.FileName} успешно сохранено");
            break;
        case MessageType.Audio:

            await SaveFile(botClient, $"{path}{update.Message.Chat.Username}", update.Message.Audio.FileId, update.Message.Audio.FileName);

            await botClient.SendTextMessageAsync(update.Message.Chat.Id, text: $"Аудио файл {update.Message.Audio.FileName} успешно сохранен");
            break;
        case MessageType.Photo:

            string time = DateTime.Today.ToString("d");
            string photoName = $"telegramphoto_{time}.png";
            await SaveFile(botClient, $"{path}{update.Message.Chat.Username}", update.Message.Photo[update.Message.Photo.Length - 1].FileId, photoName);

            await botClient.SendTextMessageAsync(update.Message.Chat.Id, text: $"Фото {photoName} успешно сохранено");
            break;
    }
}

#endregion

#region Обработка текстовых сообщений

async Task HandleMessage(ITelegramBotClient botClient, Message msg, string directory)
{
    if (System.IO.Directory.Exists(directory) == false)
    {
        System.IO.Directory.CreateDirectory(directory);
    }

    if (msg.Text == "/start")
    {
        Console.WriteLine($"{msg.Chat.Username} стартанул");
        ReplyKeyboardMarkup keyboard = new(new[]
        {
                    new KeyboardButton[] {"Список сохраненных файлов", "Получить файл из хранилища"}                    
            })
        {
            ResizeKeyboard = true
        };
    await botClient.SendTextMessageAsync(msg.Chat.Id, text: $"Здравствуйте, {msg.Chat.FirstName}. " +
        $"\nЭтот Telegram bot умеет работать с файлами\nдля сохранения файла в хранилище просто передайте их боту",
        replyToMessageId: msg.MessageId, replyMarkup: keyboard);
    return;
    }
        
    if (msg.Text == "Список сохраненных файлов")       // без кнопок
    {        
        if (System.IO.Directory.GetDirectories(directory).Length + System.IO.Directory.GetFiles(directory).Length > 0)
        {
            // Папка не пуста

            await botClient.SendTextMessageAsync(msg.Chat.Id, text: $"{msg.Chat.FirstName}, вот список сохраненных Вами файлов",
            replyToMessageId: msg.MessageId);

            var dir = new DirectoryInfo(directory); // папка с файлами

            foreach (var file in dir.GetFiles())
            {
                await botClient.SendTextMessageAsync(msg.Chat.Id, Path.GetFileName(file.FullName));
            }
        }
        else
        {
            // Папка пуста

            await botClient.SendTextMessageAsync(msg.Chat.Id, text: $"{msg.Chat.FirstName}, у Вас нет сохраненных файлов в хранилище",
            replyToMessageId: msg.MessageId);
        }

        return;
    }

    if (msg.Text == "Получить файл из хранилища")
    {
        if (System.IO.Directory.GetDirectories(directory).Length + System.IO.Directory.GetFiles(directory).Length > 0)
        {
            // Папка не пуста
            List<InlineKeyboardButton[]> list = new List<InlineKeyboardButton[]>();
       
            var dir = new DirectoryInfo(directory); // папка с файлами

            foreach (var i in dir.GetFiles())
            {            
                InlineKeyboardButton button = InlineKeyboardButton.WithCallbackData(Path.GetFileName(i.FullName), Path.GetFileName(i.FullName));
                InlineKeyboardButton[] row = new InlineKeyboardButton[1] { button };
                list.Add(row);         
            }
            var inline = new InlineKeyboardMarkup(list);
            
            await botClient.SendTextMessageAsync(msg.Chat.Id, text: $"{msg.Chat.FirstName}, вот список сохраненных Вами файлов\n" +
                $"Вы можете выбрать файл для скачивания, нажав на кнопку с именем нужного файла",
                replyMarkup: inline);
        }
        else
        {
            // Папка пуста

            await botClient.SendTextMessageAsync(msg.Chat.Id, text: $"{msg.Chat.FirstName}, у Вас нет сохраненных файлов в хранилище",
            replyToMessageId: msg.MessageId);
        }
        return;
    }
          
    await botClient.SendTextMessageAsync(msg.Chat.Id, text: $"{msg.Chat.FirstName}, выберите действие",
                 replyToMessageId: msg.MessageId);
    return;
}

#endregion

#region Обработка внутренней влавиатуры

async Task HandleCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery)
{
    var filePath = System.IO.Path.Combine(path, callbackQuery.Message.Chat.Username, callbackQuery.Data);
    if (System.IO.File.Exists(filePath))
    {
        using var stream = System.IO.File.Open(filePath, FileMode.Open);
        await botClient.SendDocumentAsync(callbackQuery.Message.Chat.Id, document: new InputOnlineFile(stream,callbackQuery.Data));
    }    

    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"Вы выбрали файл: {callbackQuery.Data}");

    return;
}

#endregion

#region Сохранение файла в локальном хранилище

async Task SaveFile(ITelegramBotClient botClient, string directory, string fileId, string fileName)
{
    var fileInfo = await botClient.GetFileAsync(fileId);
    var filePath = $"https://api.telegram.org/file/bot{token}/{fileInfo.FilePath}";
    Directory.CreateDirectory(directory);
    var path = $@"{directory}\{fileName}";
    using FileStream fs = new FileStream(path, FileMode.Create);
    await botClient.DownloadFileAsync(fileInfo.FilePath, fs);
    fs.Close();
    fs.Dispose();
    return;
}

#endregion

#region Обработка ошибок АПИ

Task HandleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken cancellationToken)
{
    var errorMessage = exception switch
    {
        ApiRequestException apiRequestException => $"Ошибка телеграм АПИ:\n{apiRequestException.ErrorCode}\n{apiRequestException.Message}",
        _ => exception.ToString()
    };
    Console.WriteLine(errorMessage);
    return Task.CompletedTask;
}

#endregion