using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Models;

namespace BalloonBot
{
    public class Program
    {
        private static IWolfClient? _client;
        private static BalloonGame? _game;

        public static async Task Main(string[] args)
        {
            Console.WriteLine("🎈 BalloonBot START");

            string email =
                Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";

            string password =
                Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("❌ WOLF_EMAIL أو WOLF_PASSWORD فارغ");
                return;
            }

            _client = new WolfClient();

            // استقبال رسائل الروم
            _client.Messaging.OnMessage += async (client, message) =>
            {
                try
                {
                    string text = message.Content?.Trim() ?? "";

                    Console.WriteLine(
                        $"📩 [{message.GroupId}] {message.UserId}: {text}"
                    );

                    if (string.IsNullOrWhiteSpace(text))
                        return;

                    // أوامر البالونات
                    if (text.StartsWith(
                        "!بالونات",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        string command =
                            text.Length > 8
                                ? text.Substring(8).Trim()
                                : "";

                        await HandleCommand(
                            client,
                            message,
                            command
                        );

                        return;
                    }

                    // اختيار رقم الخصم أو البالونة
                    if (IsNumber(text))
                    {
                        if (_game == null ||
                            !_game.Started ||
                            _game.GroupId != message.GroupId)
                            return;

                        await HandleNumber(
                            client,
                            message,
                            int.Parse(text)
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ MESSAGE ERROR:");
                    Console.WriteLine(ex);
                }
            };

            Console.WriteLine("✅ OnMessage registered");
            Console.WriteLine("🔐 Login...");

            bool login = await _client.Login(
                email,
                password
            );

            Console.WriteLine(
                "LOGIN RESULT = " + login
            );

            if (!login)
            {
                Console.WriteLine("❌ Login failed");
                return;
            }

            Console.WriteLine("✅ Login OK");
            Console.WriteLine("🟢 CONNECTED");
            Console.WriteLine("📡 BalloonBot ينتظر أوامر الروم...");

            // لا نستدعي Connect مرة ثانية
            await Task.Delay(
                Timeout.Infinite
            );
        }

        private static async Task HandleCommand(
            IWolfClient client,
            dynamic message,
            string command)
        {
            string cmd = command.Trim();

            if (cmd == "" ||
                cmd.Equals("مساعدة",
                    StringComparison.OrdinalIgnoreCase))
            {
                await client.Reply(
                    message,
                    "🎈 لعبة البالونات 🎈\n\n" +
                    "!بالونات جديد — إنشاء لعبة\n" +
                    "!بالونات انضم — الانضمام\n" +
                    "!بالونات لاعبين — عرض اللاعبين\n" +
                    "!بالونات بدء — بدء اللعبة\n" +
                    "!بالونات انهاء — إنهاء اللعبة"
                );

                return;
            }

            if (cmd.Equals(
                    "جديد",
                    StringComparison.OrdinalIgnoreCase))
            {
                _game = new BalloonGame
                {
                    GroupId = message.GroupId,
                    Started = false,
                    CurrentPlayerIndex = 0
                };

                await client.Reply(
                    message,
                    "🎈 تم إنشاء لعبة البالونات!\n\n" +
                    "كل لاعب يبدأ بـ 7 🎈\n" +
                    "اكتب !بالونات انضم للمشاركة."
                );

                return;
            }

            if (cmd.Equals(
                    "انضم",
                    StringComparison.OrdinalIgnoreCase) ||
                cmd.Equals(
                    "انضمام",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (_game == null)
                {
                    await client.Reply(
                        message,
                        "❌ لا توجد لعبة.\n" +
                        "اكتب !بالونات جديد"
                    );

                    return;
                }

                if (_game.GroupId != message.GroupId)
                    return;

                if (_game.Started)
                {
                    await client.Reply(
                        message,
                        "❌ اللعبة بدأت بالفعل."
                    );

                    return;
                }

                if (_game.Players.Any(
                    p => p.UserId == message.UserId))
                {
                    await client.Reply(
                        message,
                        "⚠️ أنت منضم مسبقاً."
                    );

                    return;
                }

                _game.Players.Add(
                    new BalloonPlayer
                    {
                        UserId = message.UserId,
                        Name = "لاعب " +
                               (_game.Players.Count + 1),
                        Balloons = 7
                    }
                );

                BalloonPlayer player =
                    _game.Players.Last();

                await client.Reply(
                    message,
                    $"🎈 تم انضمام {player.Name}\n" +
                    $"رصيدك: 7 🎈"
                );

                return;
            }

            if (cmd.Equals(
                    "لاعبين",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (_game == null)
                {
                    await client.Reply(
                        message,
                        "❌ لا توجد لعبة."
                    );

                    return;
                }

                string list = BuildPlayersList();

                await client.Reply(
                    message,
                    list
                );

                return;
            }

            if (cmd.Equals(
                    "بدء",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (_game == null)
                {
                    await client.Reply(
                        message,
                        "❌ أنشئ لعبة أولاً:\n" +
                        "!بالونات جديد"
                    );

                    return;
                }

                if (_game.GroupId != message.GroupId)
                    return;

                if (_game.Started)
                {
                    await client.Reply(
                        message,
                        "⚠️ اللعبة بدأت مسبقاً."
                    );

                    return;
                }

                if (_game.Players.Count < 2)
                {
                    await client.Reply(
                        message,
                        "❌ لازم لاعبين على الأقل حتى تبدأ اللعبة."
                    );

                    return;
                }

                _game.Started = true;
                _game.CurrentPlayerIndex = 0;
                _game.Phase = GamePhase.ChooseOpponent;

                BalloonPlayer current =
                    _game.CurrentPlayer;

                await client.GroupMessage(
                    _game.GroupId,
                    "🎈🔥 بدأت لعبة البالونات! 🔥🎈\n\n" +
                    BuildPlayersList() +
                    "\n🎯 الدور الآن: " +
                    current.Name +
                    "\n\n" +
                    "اختر رقم الخصم."
                );

                return;
            }

            if (cmd.Equals(
                    "انهاء",
                    StringComparison.OrdinalIgnoreCase) ||
                cmd.Equals(
                    "إنهاء",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (_game == null)
                {
                    await client.Reply(
                        message,
                        "❌ لا توجد لعبة."
                    );

                    return;
                }

                if (_game.GroupId != message.GroupId)
                    return;

                _game = null;

                await client.Reply(
                    message,
                    "🛑 تم إنهاء لعبة البالونات."
                );

                return;
            }

            await client.Reply(
                message,
                "❌ أمر غير معروف.\n" +
                "اكتب !بالونات مساعدة"
            );
        }

        private static async Task HandleNumber(
            IWolfClient client,
            dynamic message,
            int number)
        {
            if (_game == null)
                return;

            if (_game.GroupId != message.GroupId)
                return;

            BalloonPlayer current =
                _game.CurrentPlayer;

            // فقط اللاعب صاحب الدور يستطيع اللعب
            if (current.UserId != message.UserId)
                return;

            if (_game.Phase == GamePhase.ChooseOpponent)
            {
                if (number < 1 ||
                    number > _game.Players.Count)
                {
                    await client.Reply(
                        message,
                        "❌ رقم اللاعب غير صحيح."
                    );

                    return;
                }

                BalloonPlayer opponent =
                    _game.Players[number - 1];

                if (opponent.UserId == current.UserId)
                {
                    await client.Reply(
                        message,
                        "❌ لا يمكنك اختيار نفسك."
                    );

                    return;
                }

                if (opponent.Balloons <= 0)
                {
                    await client.Reply(
                        message,
                        "❌ هذا اللاعب خرج من اللعبة."
                    );

                    return;
                }

                _game.SelectedOpponentIndex =
                    number - 1;

                _game.Phase =
                    GamePhase.ChooseBalloon;

                await client.Reply(
                    message,
                    $"🎯 اخترت {opponent.Name}\n\n" +
                    $"عنده {opponent.Balloons} 🎈\n" +
                    $"اختر رقم البالونة من 1 إلى {opponent.Balloons}."
                );

                return;
            }

            if (_game.Phase == GamePhase.ChooseBalloon)
            {
                if (_game.SelectedOpponentIndex < 0)
                    return;

                BalloonPlayer opponent =
                    _game.Players[
                        _game.SelectedOpponentIndex
                    ];

                if (number < 1 ||
                    number > opponent.Balloons)
                {
                    await client.Reply(
                        message,
                        $"❌ اختر رقم من 1 إلى {opponent.Balloons}."
                    );

                    return;
                }

                await PopBalloon(
                    client,
                    message,
                    current,
                    opponent,
                    number
                );
            }
        }

        private static async Task PopBalloon(
            IWolfClient client,
            dynamic message,
            BalloonPlayer current,
            BalloonPlayer opponent,
            int balloonNumber)
        {
            Random random = new Random();

            int chance =
                random.Next(1, 101);

            // 15% حظ
            if (chance <= 15)
            {
                _game!.Phase =
                    GamePhase.ChooseOpponent;

                await client.GroupMessage(
                    _game.GroupId,
                    $"🍀 حظ!\n" +
                    $"{current.Name} اختار البالونة رقم {balloonNumber} " +
                    $"لكنها ما انفجرت!\n\n" +
                    "🔄 الدور ينتقل للاعب التالي."
                );

                NextTurn();

                await SendTurnMessage(
                    client
                );

                return;
            }

            // 15% نجاة
            if (chance <= 30)
            {
                _game!.Phase =
                    GamePhase.ChooseOpponent;

                await client.GroupMessage(
                    _game.GroupId,
                    $"🛡️ نجاة!\n" +
                    $"البالونة رقم {balloonNumber} نجت!\n\n" +
                    "🔄 الدور ينتقل للاعب التالي."
                );

                NextTurn();

                await SendTurnMessage(
                    client
                );

                return;
            }

            // البالونة انفجرت
            opponent.Balloons--;

            // 10% دور إضافي
            if (chance <= 40)
            {
                await client.GroupMessage(
                    _game!.GroupId,
                    $"🔄 دور إضافي!\n\n" +
                    $"💥 انفجرت البالونة رقم {balloonNumber}!\n" +
                    $"{opponent.Name}: {opponent.Balloons} 🎈"
                );

                if (opponent.Balloons <= 0)
                {
                    await EliminatePlayer(
                        client,
                        opponent
                    );

                    return;
                }

                _game.Phase =
                    GamePhase.ChooseOpponent;

                await SendTurnMessage(
                    client
                );

                return;
            }

            // 60% فرقعة عادية
            await client.GroupMessage(
                _game!.GroupId,
                $"💥 فرقعت البالونة رقم {balloonNumber}!\n\n" +
                $"{opponent.Name}: {opponent.Balloons} 🎈"
            );

            if (opponent.Balloons <= 0)
            {
                await EliminatePlayer(
                    client,
                    opponent
                );

                return;
            }

            _game.Phase =
                GamePhase.ChooseOpponent;

            NextTurn();

            await SendTurnMessage(
                client
            );
        }

        private static async Task EliminatePlayer(
            IWolfClient client,
            BalloonPlayer player)
        {
            if (_game == null)
                return;

            player.Eliminated = true;

            await client.GroupMessage(
                _game.GroupId,
                $"💥 {player.Name} فقد كل بالوناته!\n" +
                "❌ خرج من اللعبة."
            );

            int alive =
                _game.Players.Count(
                    p => !p.Eliminated
                );

            if (alive <= 1)
            {
                BalloonPlayer winner =
                    _game.Players.First(
                        p => !p.Eliminated
                    );

                await client.GroupMessage(
                    _game.GroupId,
                    $"🏆🎈 انتهت اللعبة! 🎈🏆\n\n" +
                    $"👑 الفائز: {winner.Name}\n" +
                    "🎉 مبروك!"
                );

                _game = null;
                return;
            }

            // إذا اللاعب الحالي خرج، انتقل للدور التالي
            if (_game.CurrentPlayer.Eliminated)
                NextTurn();

            _game.Phase =
                GamePhase.ChooseOpponent;

            await SendTurnMessage(
                client
            );
        }

        private static async Task SendTurnMessage(
            IWolfClient client)
        {
            if (_game == null)
                return;

            BalloonPlayer current =
                _game.CurrentPlayer;

            await client.GroupMessage(
                _game.GroupId,
                $"🎯 الدور الآن: {current.Name}\n" +
                "اختر رقم الخصم."
            );
        }

        private static void NextTurn()
        {
            if (_game == null ||
                _game.Players.Count == 0)
                return;

            int count =
                _game.Players.Count;

            for (int i = 1; i <= count; i++)
            {
                int index =
                    (_game.CurrentPlayerIndex + i)
                    % count;

                if (!_game.Players[index].Eliminated)
                {
                    _game.CurrentPlayerIndex =
                        index;

                    return;
                }
            }
        }

        private static string BuildPlayersList()
        {
            if (_game == null ||
                _game.Players.Count == 0)
            {
                return "👥 لا يوجد لاعبين.";
            }

            var lines =
                new List<string>();

            for (int i = 0;
                i < _game.Players.Count;
                i++)
            {
                BalloonPlayer p =
                    _game.Players[i];

                string status =
                    p.Eliminated
                        ? "❌ خرج"
                        : $"{p.Balloons} 🎈";

                lines.Add(
                    $"{i + 1}️⃣ {p.Name} — {status}"
                );
            }

            return
                "👥 اللاعبين:\n\n" +
                string.Join(
                    "\n",
                    lines
                );
        }

        private static bool IsNumber(
            string text)
        {
            return int.TryParse(
                text,
                out _
            );
        }
    }

    public class BalloonGame
    {
        public string GroupId { get; set; } = "";

        public bool Started { get; set; }

        public int CurrentPlayerIndex { get; set; }

        public int SelectedOpponentIndex { get; set; } = -1;

        public GamePhase Phase { get; set; }

        public List<BalloonPlayer> Players { get; set; } =
            new List<BalloonPlayer>();

        public BalloonPlayer CurrentPlayer =>
            Players[CurrentPlayerIndex];
    }

    public class BalloonPlayer
    {
        public string UserId { get; set; } = "";

        public string Name { get; set; } = "";

        public int Balloons { get; set; } = 7;

        public bool Eliminated { get; set; }
    }

    public enum GamePhase
    {
        ChooseOpponent,
        ChooseBalloon
    }
}
