using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Commands;
using WolfLive.Api.Models;

namespace BalloonBot
{
    public class Program
    {
        private static BalloonGame? _game;
        private static readonly Random _random = new Random();

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
                Console.WriteLine(
                    "❌ WOLF_EMAIL أو WOLF_PASSWORD فارغ"
                );

                return;
            }

            var client = new WolfClient()
                .SetupCommands()
                .WithCommandSet(c =>
                {
                    c.AddCommands<BalloonCommands>()
                     .WithPrefix("!");
                })
                .Done();

            client.OnConnected += (_) =>
            {
                Console.WriteLine("🟢 CONNECTED TO WOLF");
            };

            // =====================================================
            // استقبال الأرقام
            // =====================================================

            client.Messaging.OnMessage += async (c, message) =>
            {
                try
                {
                    string text =
                        message.Content?.Trim() ?? "";

                    Console.WriteLine(
                        $"📩 MESSAGE: {text}"
                    );

                    if (!int.TryParse(
                        text,
                        out int number))
                    {
                        return;
                    }

                    await HandleNumber(
                        c,
                        message,
                        number
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        "NUMBER ERROR: " +
                        ex.Message
                    );
                }
            };

            Console.WriteLine("🔐 Login...");

            bool login = await client.Login(
                email,
                password
            );

            Console.WriteLine(
                "LOGIN RESULT = " + login
            );

            if (!login)
            {
                Console.WriteLine(
                    "❌ Login failed"
                );

                return;
            }

            Console.WriteLine(
                "✅ Login OK"
            );

            Console.WriteLine(
                "🎈 BalloonBot ينتظر أوامر الروم..."
            );

            await Task.Delay(-1);
        }

        // =========================================================
        // استقبال الأرقام من الروم
        // =========================================================

        private static async Task HandleNumber(
            IWolfClient client,
            WolfMessage message,
            int number)
        {
            if (_game == null)
                return;

            if (!_game.Started)
                return;

            if (_game.GroupId != message.GroupId)
                return;

            if (number <= 0)
                return;

            BalloonPlayer current =
                _game.CurrentPlayer;

            // الرقم يجب أن يكون من اللاعب صاحب الدور
            if (current.UserId != message.UserId)
                return;

            // =====================================================
            // اختيار الخصم
            // =====================================================

            if (_game.Phase ==
                GamePhase.ChooseOpponent)
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

                if (opponent.UserId ==
                    current.UserId)
                {
                    await client.Reply(
                        message,
                        "❌ لا يمكنك اختيار نفسك."
                    );

                    return;
                }

                if (opponent.Eliminated)
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
                    $"عنده {opponent.Balloons} 🎈\n\n" +
                    $"🎈 أرسل رقم البالونة من 1 إلى {opponent.Balloons}."
                );

                return;
            }

            // =====================================================
            // اختيار البالونة
            // =====================================================

            if (_game.Phase ==
                GamePhase.ChooseBalloon)
            {
                if (_game.SelectedOpponentIndex < 0 ||
                    _game.SelectedOpponentIndex >=
                    _game.Players.Count)
                {
                    _game.Phase =
                        GamePhase.ChooseOpponent;

                    await client.Reply(
                        message,
                        "❌ حدث خطأ في اختيار الخصم.\n" +
                        "أرسل رقم الخصم من جديد."
                    );

                    return;
                }

                BalloonPlayer opponent =
                    _game.Players[
                        _game.SelectedOpponentIndex
                    ];

                if (opponent.Eliminated)
                {
                    _game.Phase =
                        GamePhase.ChooseOpponent;

                    await client.Reply(
                        message,
                        "❌ هذا اللاعب خرج من اللعبة.\n" +
                        "أرسل رقم لاعب آخر."
                    );

                    return;
                }

                if (number < 1 ||
                    number > opponent.Balloons)
                {
                    await client.Reply(
                        message,
                        $"❌ أرسل رقم من 1 إلى {opponent.Balloons}."
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

        // =========================================================
        // فرقعة البالونة
        // =========================================================

        private static async Task PopBalloon(
            IWolfClient client,
            WolfMessage message,
            BalloonPlayer current,
            BalloonPlayer opponent,
            int balloon)
        {
            int chance =
                _random.Next(1, 101);

            // =====================================================
            // 15% حظ
            // =====================================================

            if (chance <= 15)
            {
                await client.Reply(
                    message,
                    $"🍀 حظ!\n\n" +
                    $"{current.Name} اختار البالونة رقم {balloon}.\n" +
                    "لكن البالونة ما انفجرت! 🎈\n\n" +
                    "🔄 الدور ينتقل."
                );

                NextTurn();

                _game!.Phase =
                    GamePhase.ChooseOpponent;

                await ShowTurn(
                    client,
                    message
                );

                return;
            }

            // =====================================================
            // 15% نجاة
            // =====================================================

            if (chance <= 30)
            {
                await client.Reply(
                    message,
                    $"🛡️ نجاة!\n\n" +
                    $"البالونة رقم {balloon} بقيت! 🎈\n\n" +
                    "🔄 الدور ينتقل."
                );

                NextTurn();

                _game!.Phase =
                    GamePhase.ChooseOpponent;

                await ShowTurn(
                    client,
                    message
                );

                return;
            }

            // =====================================================
            // البالونة انفجرت
            // =====================================================

            opponent.Balloons--;

            // =====================================================
            // 10% دور إضافي
            // =====================================================

            if (chance <= 40)
            {
                await client.Reply(
                    message,
                    $"💥 انفجرت البالونة رقم {balloon}!\n\n" +
                    $"{opponent.Name}: {opponent.Balloons} 🎈\n\n" +
                    "🔄 حصلت على دور إضافي!"
                );

                if (opponent.Balloons <= 0)
                {
                    await Eliminate(
                        client,
                        message,
                        opponent
                    );

                    return;
                }

                _game!.Phase =
                    GamePhase.ChooseOpponent;

                await ShowTurn(
                    client,
                    message
                );

                return;
            }

            // =====================================================
            // 60% فرقعة عادية
            // =====================================================

            await client.Reply(
                message,
                $"💥 انفجرت البالونة رقم {balloon}!\n\n" +
                $"{opponent.Name}: {opponent.Balloons} 🎈"
            );

            if (opponent.Balloons <= 0)
            {
                await Eliminate(
                    client,
                    message,
                    opponent
                );

                return;
            }

            NextTurn();

            _game!.Phase =
                GamePhase.ChooseOpponent;

            await ShowTurn(
                client,
                message
            );
        }

        // =========================================================
        // إخراج اللاعب
        // =========================================================

        private static async Task Eliminate(
            IWolfClient client,
            WolfMessage message,
            BalloonPlayer player)
        {
            if (_game == null)
                return;

            player.Eliminated = true;
            player.Balloons = 0;

            await client.Reply(
                message,
                $"💥 {player.Name} انتهت بالوناته!\n" +
                "❌ خرج من اللعبة."
            );

            int alive =
                _game.Players.Count(
                    x => !x.Eliminated
                );

            // =====================================================
            // بقي لاعب واحد
            // =====================================================

            if (alive <= 1)
            {
                BalloonPlayer? winner =
                    _game.Players.FirstOrDefault(
                        x => !x.Eliminated
                    );

                if (winner != null)
                {
                    await client.Reply(
                        message,
                        "🏆🎈 انتهت اللعبة! 🎈🏆\n\n" +
                        $"👑 الفائز: {winner.Name}\n" +
                        $"🎈 بالونات متبقية: {winner.Balloons}\n\n" +
                        "🎉 مبروك!"
                    );
                }

                _game = null;

                return;
            }

            // إذا اللاعب الحالي خرج
            if (_game.CurrentPlayer.Eliminated)
            {
                NextTurn();
            }

            _game.Phase =
                GamePhase.ChooseOpponent;

            await ShowTurn(
                client,
                message
            );
        }

        // =========================================================
        // الانتقال للاعب التالي
        // =========================================================

        private static void NextTurn()
        {
            if (_game == null)
                return;

            int count =
                _game.Players.Count;

            for (int i = 1;
                 i <= count;
                 i++)
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

        // =========================================================
        // عرض الدور
        // =========================================================

        private static async Task ShowTurn(
            IWolfClient client,
            WolfMessage message)
        {
            if (_game == null)
                return;

            await client.Reply(
                message,
                $"🎯 الدور الآن: {_game.CurrentPlayer.Name}\n\n" +
                BuildPlayersList() +
                "\n\n" +
                "🎯 أرسل رقم اللاعب الذي تريد مهاجمته."
            );
        }

        // =========================================================
        // قائمة اللاعبين
        // =========================================================

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

                string status;

                if (p.Eliminated)
                {
                    status = "❌ خرج";
                }
                else
                {
                    status =
                        $"{p.Balloons} 🎈";
                }

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

        // =========================================================
        // أوامر اللعبة
        // =========================================================

        public class BalloonCommands : WolfContext
        {
            // -----------------------------------------------------
            // !بالونات
            // -----------------------------------------------------

            [Command("بالونات")]
            public async Task Help(string message)
            {
                await this.Reply(
                    "🎈🎈 لعبة البالونات 🎈🎈\n\n" +
                    "الأوامر:\n" +
                    "!بالونات جديد\n" +
                    "!بالونات انضم\n" +
                    "!بالونات لاعبين\n" +
                    "!بالونات بدء\n" +
                    "!بالونات انهاء\n\n" +
                    "🎯 طريقة اللعب:\n" +
                    "1️⃣ أرسل رقم اللاعب لاختيار الخصم.\n" +
                    "2️⃣ بعدها أرسل رقم البالونة.\n\n" +
                    "كل لاعب يبدأ بـ 7 🎈."
                );
            }

            // -----------------------------------------------------
            // !بالونات مساعدة
            // -----------------------------------------------------

            [Command("بالونات مساعدة")]
            public async Task Help2(string message)
            {
                await Help(message);
            }

            // -----------------------------------------------------
            // !بالونات جديد
            // -----------------------------------------------------

            [Command("بالونات جديد")]
            public async Task NewGame(string message)
            {
                if (_game != null)
                {
                    await this.Reply(
                        "⚠️ توجد لعبة بالونات حالياً.\n" +
                        "استخدم !بالونات انهاء أولاً."
                    );

                    return;
                }

                _game = new BalloonGame
                {
                    GroupId =
                        this.Message.GroupId,

                    Started = false,

                    CurrentPlayerIndex = 0,

                    SelectedOpponentIndex = -1,

                    Phase =
                        GamePhase.ChooseOpponent
                };

                await this.Reply(
                    "🎈 تم إنشاء لعبة البالونات! 🎈\n\n" +
                    "كل لاعب يبدأ بـ 7 🎈\n\n" +
                    "👥 للاشتراك اكتب:\n" +
                    "!بالونات انضم"
                );
            }

            // -----------------------------------------------------
            // !بالونات انضم
            // -----------------------------------------------------

            [Command("بالونات انضم")]
            public async Task Join(string message)
            {
                if (_game == null)
                {
                    await this.Reply(
                        "❌ لا توجد لعبة.\n\n" +
                        "اكتب:\n" +
                        "!بالونات جديد"
                    );

                    return;
                }

                if (_game.GroupId !=
                    this.Message.GroupId)
                {
                    return;
                }

                if (_game.Started)
                {
                    await this.Reply(
                        "❌ اللعبة بدأت بالفعل."
                    );

                    return;
                }

                if (_game.Players.Any(
                    x => x.UserId ==
                         this.Message.UserId))
                {
                    await this.Reply(
                        "⚠️ أنت منضم مسبقاً."
                    );

                    return;
                }

                string playerName =
                    "لاعب " +
                    (_game.Players.Count + 1);

                var player =
                    new BalloonPlayer
                    {
                        UserId =
                            this.Message.UserId,

                        Name =
                            playerName,

                        Balloons = 7,

                        Eliminated = false
                    };

                _game.Players.Add(
                    player
                );

                await this.Reply(
                    $"🎈 تم انضمام {player.Name}\n" +
                    "🎈 البالونات: 7"
                );
            }

            // -----------------------------------------------------
            // !بالونات انضمام
            // -----------------------------------------------------

            [Command("بالونات انضمام")]
            public async Task Join2(string message)
            {
                await Join(message);
            }

            // -----------------------------------------------------
            // !بالونات لاعبين
            // -----------------------------------------------------

            [Command("بالونات لاعبين")]
            public async Task Players(string message)
            {
                if (_game == null)
                {
                    await this.Reply(
                        "❌ لا توجد لعبة."
                    );

                    return;
                }

                if (_game.GroupId !=
                    this.Message.GroupId)
                {
                    return;
                }

                await this.Reply(
                    BuildPlayersList()
                );
            }

            // -----------------------------------------------------
            // !بالونات بدء
            // -----------------------------------------------------

            [Command("بالونات بدء")]
            public async Task StartGame(string message)
            {
                if (_game == null)
                {
                    await this.Reply(
                        "❌ أنشئ اللعبة أولاً:\n" +
                        "!بالونات جديد"
                    );

                    return;
                }

                if (_game.GroupId !=
                    this.Message.GroupId)
                {
                    return;
                }

                if (_game.Started)
                {
                    await this.Reply(
                        "⚠️ اللعبة بدأت مسبقاً."
                    );

                    return;
                }

                if (_game.Players.Count < 2)
                {
                    await this.Reply(
                        "❌ لازم لاعبين على الأقل."
                    );

                    return;
                }

                _game.Started = true;

                _game.CurrentPlayerIndex = 0;

                _game.SelectedOpponentIndex = -1;

                _game.Phase =
                    GamePhase.ChooseOpponent;

                await this.Reply(
                    "🎈🔥 بدأت لعبة البالونات! 🔥🎈\n\n" +
                    BuildPlayersList() +
                    "\n\n" +
                    $"🎯 الدور على: {_game.CurrentPlayer.Name}\n\n" +
                    "أرسل رقم اللاعب الذي تريد مهاجمته."
                );
            }

            // -----------------------------------------------------
            // !بالونات انهاء
            // -----------------------------------------------------

            [Command("بالونات انهاء")]
            public async Task EndGame(string message)
            {
                if (_game == null)
                {
                    await this.Reply(
                        "❌ لا توجد لعبة."
                    );

                    return;
                }

                if (_game.GroupId !=
                    this.Message.GroupId)
                {
                    return;
                }

                _game = null;

                await this.Reply(
                    "🛑 تم إنهاء لعبة البالونات."
                );
            }

            // -----------------------------------------------------
            // !بالونات إنهاء
            // -----------------------------------------------------

            [Command("بالونات إنهاء")]
            public async Task EndGame2(string message)
            {
                await EndGame(message);
            }
        }

        // =========================================================
        // حالة اللعبة
        // =========================================================

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

        // =========================================================
        // اللاعب
        // =========================================================

        public class BalloonPlayer
        {
            public string UserId { get; set; } = "";

            public string Name { get; set; } = "";

            public int Balloons { get; set; } = 7;

            public bool Eliminated { get; set; }
        }

        // =========================================================
        // مراحل اللعبة
        // =========================================================

        public enum GamePhase
        {
            ChooseOpponent,
            ChooseBalloon
        }
    }
}
