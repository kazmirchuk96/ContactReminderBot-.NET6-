﻿using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ContactReminderBot_NET6_
{
    internal class Program
    {
        static TelegramBotClient bot = new("5421750660:AAEfthKF3TMo-9Uw6Ey-LI1NsXdpQ1Qk3SI");
        static List<TelegramGroup> listGroups = new();
        
        //-----------------------------------------------------------------------------------------

        private static bool newGroup; //пользователь добавил новую группу, ожидаем шаблон
        private static bool waitingNumbersForRemind; //ожидаем что пользователь введёт числа групп которым будем напоминать
        private static bool waitingNumberGroupForTemplate; //ожидаем, что пользователь введёт номер группы, шаблон которой он хочет просмотреть/изменить 
        private static bool waitingNewTemplate; //ожидаем, что пользователь введёт новый шаблон
        private static bool waitingNumberGroupForDeleting; //ожидаем, что пользователь введёт номер группы, которую нужно удалить
        private static bool waitingNumberGroupForFreeMessage;//ожидаем, что пользователь введёт номера групп, которым нужно отправить свободное сообщение
        private static bool waitingTextOfFreeMessage;//ожидаем, что пользователь введёт текст своободного сообщения
        private static bool autopilotMode = false;

        //-----------------------------------------------------------------------------------------

        private const string fileName = @"groups.json";//поменять месторасположение файла
        //const long managerChatId = 5117974777;//chat id рабочего телеграмма
        private const long managerChatId = 347327196; //chat id моего личного телеграма
        private static long groupId;
        private static string[]? numberGroupsForFreeMessage;//номера групп, которым будем отправлять свободное сообщение
        private static int messageIdtest;

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,CancellationToken cancellationToken)
        {
            var group = new TelegramGroup();

            if (update.Type == UpdateType.Message)
            {
                var message = update.Message;

                //если файл не существует, то создаем его, в него будем записывать список групп
                if (!System.IO.File.Exists(fileName))
                {
                    await System.IO.File.WriteAllTextAsync(fileName, string.Empty, cancellationToken);
                }

                listGroups = GettingListOfGroups();//запись в лист списка групп из файла

                if (message?.Text != null && message.Text.ToLower() == "/start")//введена команда start
                {
                    /* ждём от пользователя каку-то информацию (номер группы, текст шаблона, сообщение), но он вводит текущую команду, поэтому ставим false флагу с ожиданием*/
                    FlagManaging();

                    if (message.Chat.Title != null)
                    {
                        //Проверка на наличие этой же группы в списке, если нет до добавляем
                        if (listGroups.Where(x => x.Name == message.Chat.Title).ToList().Count != 0)
                        {
                            await botClient.SendTextMessageAsync(managerChatId, $"❌ Група \"{message.Chat.Title}\" вже існує в списку груп\n\nПереглядати шаблони груп та змінювати їх ти можеш використовуючи команду /template");
                        }
                        else
                        {
                            group = new TelegramGroup(message.Chat.Id, message.Chat.Title);
                            listGroups.Add(group);//добавляем группу в List
                            WritingListOfGroupsToFile();//запись списка групп в JsonFile
                            await botClient.SendTextMessageAsync(managerChatId, $"✅ Група \"{message.Chat.Title}\" успішно додана!\n\nНадішли шаблон повідомлення, використовуючин наступні коди:\n\n[smile] – смайлик\n[greeting] – привітання+смайлик\n[date] – дата завтра\n[waitingphrase] – фраза в кінці повідомлення + смайлик");
                            newGroup = true;
                        }
                    }
                }
                else if (message?.Text != null && message.Chat.Id == managerChatId && newGroup)//пользователь добавил группу, ждём от него шаблон для напоминание
                {
                    listGroups[^1].TextTemplate = message.Text;
                    WritingListOfGroupsToFile();//запись списка групп в JsonFile
                    await botClient.SendTextMessageAsync(message.Chat, $"✅ Шаблон успішно доданий! Нижче направляю приклад повідомлення згідно твого шаблону.\n\nПереглядати шаблони груп та змінювати їх ти можеш використовуючи команду /template", cancellationToken: cancellationToken);
                    await botClient.SendTextMessageAsync(message.Chat, TextForReminding(message.Text), cancellationToken: cancellationToken);
                    newGroup = false;
                }
                else if (message?.Text != null && message.Chat.Id == managerChatId &&
                         message.Text == "/remind") //отправляем напоминание 
                {
                    /* ждём от пользователя каку-то информацию (номер группы, текст шаблона, сообщение), но он вводит текущую команду, поэтому ставим false флагу с ожиданием*/
                    FlagManaging();

                    Message sentMessage = await botClient.SendTextMessageAsync(
                        chatId: message.Chat,
                        text: "Обирай групи, яким необхідно відправити повідомлення або керуй режимом автопілота😊",
                        replyMarkup: KeyboardWithGroupsDays(),
                        cancellationToken: cancellationToken);
                     messageIdtest = sentMessage.MessageId;
                    

                    waitingNumbersForRemind = true;
                }
                else if (message?.Text != null && message.Chat.Id == managerChatId && message.Text == "/template")
                {
                    /* ждём от пользователя каку-то информацию (номер группы, текст шаблона, сообщение), но он вводит текущую команду, поэтому ставим false флагу с ожиданием*/
                    FlagManaging();

                    string outputMessage = "Введи номер групи шаблон якої ти хочеш переглянути/змінити:\n\n";
                    await botClient.SendTextMessageAsync(message.Chat, outputMessage + OutputListOgGroups(), cancellationToken: cancellationToken);
                    waitingNumberGroupForTemplate = true;
                }
                else if (message?.Text != null && message.Chat.Id == managerChatId && message.Text == "/delete")
                {
                    /* ждём от пользователя каку-то информацию (номер группы, текст шаблона, сообщение), но он вводит текущую команду, поэтому ставим false флагу с ожиданием*/
                    FlagManaging();
                    string outputMessage = "Введи номер групи, яку ти хочеш видалити:\n\n";
                    await botClient.SendTextMessageAsync(message.Chat, outputMessage + OutputListOgGroups(), cancellationToken: cancellationToken);
                    waitingNumberGroupForDeleting = true;
                }
                else if (message?.Text != null && message.Chat.Id == managerChatId && message.Text == "/message")//отправка свободного сообщения в группы
                {
                    /* ждём от пользователя каку-то информацию (номер группы, текст шаблона, сообщение), но он вводит текущую команду, поэтому ставим false флагу с ожиданием*/
                    FlagManaging();
                    string outputMessage = "Введи номери груп через кому (1, 2, 3), яким необхідно відправити повідомлення:\n\n";
                    await botClient.SendTextMessageAsync(message.Chat, outputMessage + OutputListOgGroups(), cancellationToken: cancellationToken);
                    waitingNumberGroupForFreeMessage = true;
                }
                else if (message?.Text != null && message.Chat.Id == managerChatId && waitingNumberGroupForFreeMessage)
                {
                    if (СheckingForCorrectInput(message.Text))
                    {
                        numberGroupsForFreeMessage = message.Text.Split(',');
                        waitingNumberGroupForFreeMessage = false;
                        waitingTextOfFreeMessage = true;
                        await botClient.SendTextMessageAsync(message.Chat, "✏️ Введи текст повідомлення, який хочеш відправити", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(message.Chat, $"❌ Повтори введення", cancellationToken: cancellationToken);
                    }
                }
                else if (message?.Text != null && message.Chat.Id == managerChatId && waitingTextOfFreeMessage)
                {
                    for (var i = 0; i < numberGroupsForFreeMessage.Length; i++)
                    {
                        if (int.Parse(numberGroupsForFreeMessage[i]) <= listGroups.Count && int.Parse(numberGroupsForFreeMessage[i]) != 0)
                        {
                            group = listGroups[int.Parse(numberGroupsForFreeMessage[i]) - 1];
                            await botClient.SendTextMessageAsync(message.Chat, $"✅ Повідомлення в групу \"{group.Name}\" успішно відправлено", cancellationToken: cancellationToken);
                            await botClient.SendTextMessageAsync(group.ID, message.Text, cancellationToken: cancellationToken);
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(message.Chat, $"❌ Не існує групи з номером {int.Parse(numberGroupsForFreeMessage[i])}", cancellationToken: cancellationToken);
                        }
                    }
                    waitingTextOfFreeMessage = false;
                }
                else if (message?.Text != null && message.Chat.Id == managerChatId && waitingNumberGroupForTemplate)
                {
                    await botClient.SendTextMessageAsync(message.Chat, $"Шаблон цієї групи:", cancellationToken: cancellationToken);
                    await botClient.SendTextMessageAsync(message.Chat, listGroups[int.Parse(message.Text)-1].TextTemplate, cancellationToken: cancellationToken);
                    await botClient.SendTextMessageAsync(message.Chat, $"Для зміни шаблону відправ у відповідь новий шаблон повідомлення, використовуючин наступні коди:\n\n[smile] – смайлик\n[greeting] – привітання+смайлик\n[date] – дата завтра\n[waitingphrase] – фраза в кінці повідомлення + смайлик", cancellationToken: cancellationToken);
                    waitingNumberGroupForTemplate = false;
                    waitingNewTemplate = true;
                    groupId = listGroups[int.Parse(message.Text) - 1].ID;
                }
                else if (message?.Text != null && message.Chat.Id == managerChatId && waitingNewTemplate)
                {
                    listGroups.First(x => x.ID == groupId).TextTemplate = message.Text;
                    WritingListOfGroupsToFile();
                    waitingNewTemplate = false;
                    await botClient.SendTextMessageAsync(message.Chat, $"✅ Шаблон успішно змінений! Нижче направляю приклад повідомлення згідно твого шаблону.\n\nВідправити нагадування ти можеш використовуючи команду /remind", cancellationToken: cancellationToken);
                    await botClient.SendTextMessageAsync(message.Chat, TextForReminding(message.Text), cancellationToken: cancellationToken);
                }
                else if (message.Text != null && message.Chat.Id == managerChatId && waitingNumberGroupForDeleting)
                {
                    listGroups.Remove(listGroups[int.Parse(message.Text) - 1]);
                    waitingNumberGroupForDeleting = false;
                    WritingListOfGroupsToFile();
                    await botClient.SendTextMessageAsync(message.Chat, $"✅ Група успішно видалена", cancellationToken: cancellationToken);
                }
                else if (message.Text != null && message.Chat.Id == managerChatId)
                {
                    await botClient.SendTextMessageAsync(message.Chat, $"Виберіть одну із команд:\n\n/remind - Нагадування про заняття по заданому шаблону\n/template - Перегляд та зміна шаблону\n/message - Відправлення текстового повідомлення\n/delete - Видалення груп", cancellationToken: cancellationToken);
                }
            }
            
            if (update.CallbackQuery !=null) //если была нажата одна из кнопок c названием группы
            {
                string? data = update.CallbackQuery.Data;
                data = data.ToLower();

                if (waitingNumbersForRemind)
                {
                    if (data is "пт" or "сб" or "нд" or "чт") //пользователь хочет отправить сообщение во все группы, которые учатся в конкретный день
                    {
                        foreach (var item in listGroups)
                        {
                            if (item.Name.ToLower().Contains(data))
                            {
                                await botClient.SendTextMessageAsync(managerChatId,
                                    $"✅ Повідомлення в групу \"{item.Name}\" успішно відправлено",
                                    cancellationToken: cancellationToken);
                                await botClient.SendTextMessageAsync(item.ID, TextForReminding(item.TextTemplate),
                                    cancellationToken: cancellationToken);
                            }
                        }
                    }
                    else if (data == "manual") //выводим клавиатуру со списко групп, чтоб пользователь мог выбрать конкретную группу
                    {
                        Message sentMessage = await botClient.SendTextMessageAsync(
                            chatId: managerChatId,
                            text: "Обери групи, яким необхідно відправити повідомлення про заняття😊",
                            replyMarkup: KeyboardWithGroups(),
                            cancellationToken: cancellationToken);
                    }
                    else if (data == "autopiloton")
                    {
                        autopilotMode = true;
                        botClient.EditMessageReplyMarkupAsync(managerChatId, messageIdtest,KeyboardWithGroupsDays(),cancellationToken);//изменение меню
                        
                        await botClient.AnswerCallbackQueryAsync(
                            callbackQueryId: update.CallbackQuery.Id,
                            text: $"Режим автопілота ввімкнено! Нагадування беру на себе😉",
                            showAlert: true);

                    }
                    else if (data == "autopilotoff")
                    {
                        autopilotMode = false;
                        botClient.EditMessageReplyMarkupAsync(managerChatId, messageIdtest, KeyboardWithGroupsDays(), cancellationToken);//изменение меню

                        await botClient.AnswerCallbackQueryAsync(
                            callbackQueryId: update.CallbackQuery.Id,
                            text: $"Режим автопілота вимкнено! Тепер ти робиш нагадування самостійно😉",
                            showAlert: true);
                    }
                    else//пользователь отправляет сообщеие конкретной группе
                    {
                        group = listGroups.FirstOrDefault(x => x.Name.ToLower() == data);
                        await botClient.SendTextMessageAsync(managerChatId, $"✅ Повідомлення в групу \"{group.Name}\" успішно відправлено", cancellationToken: cancellationToken);
                        await botClient.SendTextMessageAsync(group.ID, TextForReminding(group.TextTemplate), cancellationToken: cancellationToken);
                    }
                }
            }
        }

        public static bool СheckingForCorrectInput(string? inputMessage)
        {
            const string allowedSymbols = "01234567890,";
            bool inputMessageIsCorrect = true;
            if (inputMessage != null)
            {
                inputMessage = Regex.Replace(inputMessage, @"\s+", " ");//удаление лишних пробелов
                inputMessage = Regex.Replace(inputMessage, @",+", ",");//заменяем 2 запятые на одну
                inputMessage = Regex.Replace(inputMessage, @",$", "");//удаляем запятую с конца строки
                inputMessage = Regex.Replace(inputMessage, @"^,", "");//удаляем запятую с начала строки

                //проверка на правлиьность ввода в строке должны быть только числа и запятая - вынести в метод
                foreach (var symbol in inputMessage)
                {
                    if (!allowedSymbols.Contains(symbol))
                    {
                        inputMessageIsCorrect = false;
                        break;
                    }
                }
            }
            return inputMessageIsCorrect;
        }

        //текст для напоминание, где вместо кодов подставляется соответствующая информация
        public static string TextForReminding(string textTamplateWithCodes)
        {
            string[] smilesForGreetings = {"","😀","😁","☺️","😊","🙂","😍","😜","🙃","🤓","😎","🤩","🤖","👾","👻","😺","😻","🙌","🤝","✌️","🤟","✋","🖐","👋","🦾","🐼"};
            string[] smilesForMaintText = {"🔹","🔸","✅","➡️","👉","👨‍💻","✨","🚀","📕","📗","📘","📙","📒","✅","▶️","➡️","📍","🖥","💻","✏️","⭕️","🔵"};
            string[] smilesForWaitiongPhrase = { "", "💙💛", "💙","💛","💜","💚","🧡","❤️","😉","👌","🫶","👐","👍","🤗","😘","💪"};
            

            /*Вынести в файл*/
            string[] greetingsFirstPart = new []
            {
                "Хелоу!",
                "Хелоу, еврібаді!",
                "Тук-тук!",
                "Добрий день, everybody!",
                "Вітаю вас, земляни!",
                "Алоха!",
                "Бонжур!",
                "Привітики!",
                "Привіт!",
                "Всім привіт!",
                "Вітаю!",
                "Хола!",
                "Салют!",
                "Як настрій?",
                "Як і обіцяв, ось і я!)",
                "Хай!",
                "Ватсап!",
                "Доброго дня!",
                "Добрий день!",
                "Hello!",
                ""
            };

            string[] greetingsSecondPart = new[]
            {
                "Я бот-помічник IT Академії CONTACT!",
                "На зв'язку бот-помічник IT Академії CONTACT!",
                "Я телеграм-бот помічник IT Академії CONTACT!",
                "Я віртуальний бот-помічник IT Академії CONTACT!",
                "На зв'язку телеграм-бот IT Академії CONTACT!"
            };
            string[] waitingPhrases = new[]
            {
                "Всіх чекаю!",
                "Всіх з нетерпінням чекаю!",
                "До зустрічі!",
                "Не забуваємо робити домашку!",
                "Що там з домашкою?",
                "Надіюсь, що ви вже зробили домашку!",
                "Як справи з домашкою?",
                "До завтра!",
                "Всім гарного дня!",
                "Не забудьте про домашку!",
                "Всіх чекаю на занятті!"
            };

            Random rand = new Random();

            //замена кода [greetings] на приветствие и смайлик
            string finalText = textTamplateWithCodes.Replace("[greeting]", greetingsFirstPart[rand.Next(0, greetingsFirstPart.Length)] + " "+ greetingsSecondPart[rand.Next(0, greetingsSecondPart.Length)] + smilesForGreetings[rand.Next(0,smilesForGreetings.Length)]);

            //замена кода [waitingphrase] на фразу и смайлик
            finalText = finalText.Replace("[waitingphrase]", waitingPhrases[rand.Next(0, waitingPhrases.Length)] + smilesForWaitiongPhrase[rand.Next(0,smilesForWaitiongPhrase.Length)]);

            //замена кода [smile], делаем в цикле, потому что нужно чтоб все смайлики были разными
            finalText = finalText.Replace("[smile]", "smile");//убмраем [], потому что это элемент регулярного выражения
            while (finalText.Contains("smile"))
            {
                /*Регулярное выражение, которое заменяет первое вхождение [smile] на смайлик*/
                Regex reg = new Regex("smile");
                finalText = reg.Replace(finalText, smilesForMaintText[rand.Next(0,smilesForMaintText.Length)]+" ", 1);
            }

            //замена кода [date] на завтрашнюю дату
            var today = DateTime.Today;
            string tomorrow = today.AddDays(1).ToShortDateString();

            finalText = finalText.Replace("[date]", $"({tomorrow.Remove(tomorrow.Length-5)})");//замена на звтрашнюю дату без года

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

        //метод который формирует строку из списка групп с номерами для выывода
        public static string OutputListOgGroups()
        {
            string outputMessage = string.Empty;
            foreach (var item in listGroups)
            {
                outputMessage += $"{listGroups.IndexOf(item) + 1} – {item.Name}\n";
            }

            return outputMessage;
        }

        //метод который формирует список клавиш с названиями дней занятия
        public static InlineKeyboardMarkup KeyboardWithGroupsDays()
        {
            
            var array = (autopilotMode)?new InlineKeyboardButton[1][]:new InlineKeyboardButton[3][];

            if (autopilotMode)
            {
                array[0] = new[]
                {
                    InlineKeyboardButton.WithCallbackData("Вимкнути режим автопілота❌", "autopilotoff")
                };
            }
            else
            {
                array[0] = new[]
                {
                    //InlineKeyboardButton.WithCallbackData("ЧТ", "ЧТ"),
                    InlineKeyboardButton.WithCallbackData("ПТ", "ПТ"),
                    InlineKeyboardButton.WithCallbackData("СБ", "СБ"),
                    InlineKeyboardButton.WithCallbackData("НД", "НД")
                };

                array[1] = new[]
                {
                    InlineKeyboardButton.WithCallbackData("Обрати в ручному режимі", "manual"),
                    InlineKeyboardButton.WithCallbackData("Відправити всім", "all"),
                };

                array[2] = new[]
                {
                    InlineKeyboardButton.WithCallbackData("Ввімкнути режим автопілота✈️", "autopiloton"),
                };
            }
            InlineKeyboardMarkup inlineKeyboard = new(array);
            return inlineKeyboard;
        }

        //метод который формирует список клавиш с названиями групп занятия
        public static InlineKeyboardMarkup KeyboardWithGroups()
        {
            var array = new InlineKeyboardButton[listGroups.Count][];
            
            for (int i = 0; i < listGroups.Count; i++)
            {
                array[i] = new InlineKeyboardButton[1];
                array[i][0] = InlineKeyboardButton.WithCallbackData(listGroups[i].Name, listGroups[i].Name);
            }

            InlineKeyboardMarkup inlineKeyboard = new(array);
            return inlineKeyboard;
        }
        public static void FlagManaging()
        {
            waitingNumberGroupForTemplate = false; //ждём от пользователя номер числа для просмотра шаблона, но он вводит одну из команд
            waitingNumbersForRemind = false; //ждём от пользователя номера чисел для напоминаний, но он вводит одну из команд
            newGroup = false;//ждём от пользователя шаблон для напоминания, но он вводит одну из команд
            waitingNewTemplate = false; //ждём от пользователя новый шаблон (в случае если он захотел его изменить), но он вводит одну из команд
            waitingNumberGroupForDeleting = false; //ждём от пользователя номер группы, которую нужно удалить, но он вводит одну из команд
            waitingNumberGroupForFreeMessage = false;//ждём от пользователя номера, групп, которым нужно отправить свободное соощение, но он вводит одну из команд
            waitingTextOfFreeMessage = false;//ждём от пользователя текст свободного сообщения, но он вводит одну из команд
        }
        public static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            // Некоторые действия
            Console.WriteLine(JsonConvert.SerializeObject(exception));
            return Task.CompletedTask;
        }
        static void Main()
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