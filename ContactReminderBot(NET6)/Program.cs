using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ContactReminderBot_NET6_
{
    internal class Program
    {
        static TelegramBotClient bot = new("5421750660:AAEfthKF3TMo-9Uw6Ey-LI1NsXdpQ1Qk3SI");
        static List<TelegramGroup> listGroups = new();
        
        //-----------------------------------------------------------------------------------------

        static bool newGroup; //пользователь добавил новую группу, ожидаем шаблон
        static bool waitingNumbersForRemind; //ожидаем что пользователь введёт числа групп которым будем напоминать
        static bool waitingNumberGroupForTemplate; //ожидаем, что пользователь введёт номер группы, шаблон которой он хочет просмотреть/изменить 
        static bool waitingNewTemplate; //ожидаем, что пользователь введёт новый шаблон

        //-----------------------------------------------------------------------------------------

        const string fileName = @"groups.json";
        //const long managerChatId = -611718767;//chatID канала через который управляем ботом
        //const long managerChatId = 5117974777;//chat id рабочего телеграмма
        const long managerChatId = 347327196; //chat id моего личного телеграма

        const string numbers = "01234567890,";

        static long groupId;

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,CancellationToken cancellationToken)
        {
            var group = new TelegramGroup();

            if (update.Type == Telegram.Bot.Types.Enums.UpdateType.Message)
            {
                var message = update.Message;

                //если файл не существует, то создаем его, в него будем записывать список групп
                if (!System.IO.File.Exists(fileName))
                {
                    System.IO.File.WriteAllText(fileName, string.Empty);
                }

                listGroups = GettingListOfGroups();//запись в лист списка групп из файла

                if (message.Text != null && message.Text.ToLower() == "/start")//введена команда start
                {
                    waitingNumberGroupForTemplate = false; //ждём от пользователя номер числа для просмотра шаблона, но он вводит /start
                    waitingNumbersForRemind = false; //ждём от пользователя номера чисел для напоминаний, но он вводит /start
                    newGroup = false;//ждём от пользователя шаблон для напоминания, но он вводит /start
                    waitingNewTemplate = false; //ждём от пользователя новый шаблон (в случае если он захотел его изменить), но он вводит /start

                    if (message.Chat.Title != null)
                    {
                        group = new TelegramGroup(message.Chat.Id, message.Chat.Title);
                        listGroups.Add(group);//добавляем группу в List
                        WritingListOfGroupsToFile();//запись списка групп в JsonFile
                        await botClient.SendTextMessageAsync(managerChatId, $"Група \"{message.Chat.Title}\" успішно додана ✅ \n\nНадішли шаблон повідомлення, використовуючин наступні коди:\n\n[smile] – смайлик\n[greeting] – привітання\n[date] – дата завтра\n[waitingphrase] – фраза, що ставитимется в кінці повідомлення (нариклад, \"Всії чекаємо!\"");
                        newGroup = true;
                    }
                }
                else if (message.Chat.Id == managerChatId && newGroup)//пользователь добавил группу, ждём от него шаблон для напоминание
                {
                    if (listGroups != null) listGroups[listGroups.Count - 1].TextTemplate = message.Text;
                    WritingListOfGroupsToFile();//запись списка групп в JsonFile
                    await botClient.SendTextMessageAsync(message.Chat, $"Шаблон успішно доданий! Переглядати шаблони та змінювати їх ти можеш використовуючи команду /template");
                    newGroup = false;
                }
                else if (message.Chat.Id == managerChatId && message.Text == "/remind")//отправляем напоминание 
                {
                    waitingNumberGroupForTemplate = false; //ждём от пользователя номер числа для просмотра шаблона, но он вводит /remind
                    waitingNumbersForRemind = false; //ждём от пользователя номера чисел для напоминаний, но он вводит /remind
                    newGroup = false;//ждём от пользователя шаблон для напоминания, но он вводит /remind
                    waitingNewTemplate = false; //ждём от пользователя новый шаблон (в случае если он захотел его изменить), но он вводит /remind
                    
                    string outputMessage = "Введи номери груп через кому (1, 2, 3), яким необхідно відправити нагадування про заняття:\n\n";
                    
                    if (listGroups != null)
                    {
                        foreach (var item in listGroups)
                        {
                            outputMessage += $"{listGroups.IndexOf(item) + 1} – {item.Name}\n";
                        }
                        await botClient.SendTextMessageAsync(message.Chat, outputMessage);
                    }
                    waitingNumbersForRemind = true;
                }
                else if (message.Chat.Id == managerChatId && waitingNumbersForRemind && message.Text != "/template" && message.Text !="/start" && message.Text!="/remind")
                {
                    bool inputMessageIsCorrect = true;
                    string inputMessage = message.Text;//строка с номерами групп, которым будем напоминать "1,2,3,4,5"
                    
                    for (int i = 0; i < inputMessage.Length / 2; i++)//удаляем лишние пробелы
                    {
                        inputMessage = inputMessage.Replace(" ", "");
                    }

                    //проверка на правлиьность ввода в строке должны быть только числа и запятая - вынести в метод
                    foreach (var symbol in inputMessage)
                    {
                        if (!numbers.Contains(symbol))
                        {
                            await botClient.SendTextMessageAsync(message.Chat, $"Повторіть введення");
                            inputMessageIsCorrect = false;
                            break;
                        }
                    }

                    if (inputMessageIsCorrect)
                    {
                        var arrayNumbers = inputMessage.Split(',');

                        for (int i = 0; i < arrayNumbers.Length; i++)
                        {
                            group = listGroups[int.Parse(arrayNumbers[i]) - 1];
                            await botClient.SendTextMessageAsync(message.Chat, $"Повідомлення в групу \"{group.Name}\" успішно відправлено");
                            await botClient.SendTextMessageAsync(group.ID, TextForReminding(group.TextTemplate),ParseMode.Html);
                        }
                        waitingNumbersForRemind = false;
                    }
                }
                else if (message.Chat.Id == managerChatId && message.Text == "/template")
                {
                    waitingNumberGroupForTemplate = false; //ждём от пользователя номер числа для просмотра шаблона, но он вводит /template
                    waitingNumbersForRemind = false; //ждём от пользователя номера чисел для напоминаний, но он вводит /template
                    newGroup = false;//ждём от пользователя шаблон для напоминания, но он вводит /template
                    waitingNewTemplate = false; //ждём от пользователя новый шаблон (в случае если он захотел его изменить), но он вводит /template

                    string outputMessage = "Введи номер групи шаблон якої ти хочеш переглянути/змінити:\n\n";

                    if (listGroups != null)
                    {
                        foreach (var item in listGroups)
                        {
                            outputMessage += $"{listGroups.IndexOf(item) + 1} – {item.Name}\n";
                        }
                        await botClient.SendTextMessageAsync(message.Chat, outputMessage);
                    }

                    waitingNumberGroupForTemplate = true;
                }
                else if (message.Chat.Id == managerChatId && waitingNumberGroupForTemplate)
                {
                    await botClient.SendTextMessageAsync(message.Chat, $"Шаблон цієї групи:");
                    await botClient.SendTextMessageAsync(message.Chat, listGroups[int.Parse(message.Text)-1].TextTemplate);
                    await botClient.SendTextMessageAsync(message.Chat, $"Для зміни шаблону відправ у відповідь новий");
                    waitingNumberGroupForTemplate = false;
                    waitingNewTemplate = true;
                    groupId = listGroups[int.Parse(message.Text) - 1].ID;
                }
                else if (message.Chat.Id == managerChatId && waitingNewTemplate)
                {
                    group = listGroups.Where(x => x.ID == groupId).First();
                    group.TextTemplate = message.Text;
                    WritingListOfGroupsToFile();
                    waitingNewTemplate = false;
                    await botClient.SendTextMessageAsync(message.Chat, $"Шаблон групи \"{group.Name}\" успішно змінений");
                }
            }
        }

        //текст для напоминание, где вместо кодов подставляется соответствующая информация

        public static string TextForReminding(string textTamplateWithCodes)
        {
            string smilesForGreetings = " 😊☀️😉👋😀🤩😁😃☺️🙂😉🤓👐🙌🤝🖐👋🤗✌";

            /*var tes1 = Char.ConvertToUtf32('😊');
            string tes2 = "😊";
            var ccc = '\U000026C4';*/


            string smilesForMaintText = "🔹🔸✅👨‍💻➡️👉👨‍💻👩‍💻🚀💻❇️▪️";
            string[] smilesForWaitiongPhrase = new[] { "💙💛", "💙", "💛", "😊","😉","🤩","😀","😁","💪","🐼","☺️","😌","😉","🙂","👐","🙌","🤗","😺","✌️","👩","‍💻","🧑","‍💻","👨","‍💻","🚀" };
            
            string[] greetings = new [] { "Всім привіт!", "Доброго дня!", "Привіт!", "Добрий день!"};
            string[] waitingPreases = new[] { "Всіх чекаємо!", "Всіх з нетерпінням чекаємо!", "Всіх чекатимемо!", "Чекаємо вас!", "З нетерпінням чекаємо вас!"};

            Random rand = new Random();

            //замена кода [greetings] на приветствие и смайлик
            string finalText = textTamplateWithCodes.Replace("[greeting]", greetings[rand.Next(0, greetings.Length)]);

            return finalText;
        }
        
        //запись списка групп в Json файл
        public static void WritingListOfGroupsToFile()
        {
            var data = JsonConvert.SerializeObject(listGroups);
            System.IO.File.WriteAllText(fileName, data);
        }

        //получения листа груп из файла
        public static List<TelegramGroup> GettingListOfGroups()
        {
            var data = System.IO.File.ReadAllText(fileName);
            listGroups = (data != string.Empty) ? JsonConvert.DeserializeObject<List<TelegramGroup>>(data) : new List<TelegramGroup>();
            return listGroups;
        }
        public static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            // Некоторые действия
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(exception));
            return Task.CompletedTask;
        }
        static void Main(string[] args)
        {
            Console.WriteLine("Запущен бот " + bot.GetMeAsync().Result.FirstName);

            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            var receiverOptions = new ReceiverOptions();
            bot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken
            );
            Console.ReadLine();
        }
    }
}