using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Commands;

namespace BalloonBot
{
    public class Program
    {
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
                Console.WriteLine("❌ Login failed");
                return;
            }

            Console.WriteLine("✅ Login OK");
            Console.WriteLine("🎈 BalloonBot ينتظر أوامر الروم...");

            await Task.Delay(-1);
        }

        // =========================================================
        // أوامر لعبة البالونات
        // =========================================================

        public class BalloonCommands : WolfContext
        {
            [Command("بالونات")]
            public async Task Help(string message)
            {
                await Reply(
                    "🎈🎈 لعبة البالونات 🎈🎈\n\n" +
                    "!بالونات جديد\n" +
                    "!بالونات انضم\n" +
                    "!بالونات لاعبين\n" +
                    "!بالونات بدء\n" +
                    "!بالونات انهاء\n\n" +
                    "بعد بدء اللعبة:\n" +
                    "أرسل رقم اللاعب لاختيار الخصم.\n" +
                    "ثم أرسل رقم البالونة."
                );
            }

            [Command("بالونات مساعدة")]
            public async Task Help2(string message)
            {
                await Help(message);
            }

            [Command("بالونات جديد")]
            public async Task NewGame(string message)
            {
                _game = new BalloonGame
                {
                    GroupId = this.Message.GroupId,
                    Started = false,
                    CurrentPlayerIndex = 0
                };

                await Reply(
                    "🎈 تم إنشاء لعبة البالونات!\n\n" +
                    "كل لاعب يبدأ بـ 7 🎈\n" +
                    "اكتب:\n" +
                    "!بالونات انضم"
                );
            }

            [Command("بالونات انضم")]
            public async Task Join(string message)
            {
                if (_game == null)
                {
                    await Reply(
                        "❌ لا توجد لعبة.\n" +
                        "اكتب !بالونات جديد"
                    );
                    return;
                }

                if (_game.Started)
                {
                    await Reply(
                        "❌ اللعبة بدأت بالفعل."
                    );
                    return;
                }

                if (_game.GroupId != this.Message.GroupId)
                    return;

                if (_game.Players.Any(
                    x => x.UserId == this.Message.UserId))
                {
                    await Reply(
                        "⚠️ أنت منضم مسبقاً."
                    );
                    return;
                }

                var player = new BalloonPlayer
                {
                    UserId = this.Message.UserId,
                    Name = "لاعب " +
                           (_game.Players.Count + 1),
                    Balloons = 7
                };

                _game.Players.Add(player);

                await Reply(
                    $"🎈 تم انضمام {player.Name}\n" +
                    "🎈 البالونات: 7"
                );
            }

            [Command("بالونات انضمام")]
            public async Task Join2(string message)
            {
                await Join(message);
            }

            [Command("بالونات لاعبين")]
            public async Task Players(string message)
            {
                if (_game == null)
                {
                    await Reply(
                        "❌ لا توجد لعبة."
                    );
                    return;
                }

                await Reply(
                    BuildPlayersList()
                );
            }

            [Command("بالونات بدء")]
            public async Task StartGame(string message)
            {
                if (_game == null)
                {
                    await Reply(
                        "❌ أنشئ اللعبة أولاً:\n" +
                        "!بالونات جديد"
                    );
                    return;
                }

                if (_game.GroupId != this.Message.GroupId)
                    return;

                if (_game.Started)
                {
                    await Reply(
                        "⚠️ اللعبة بدأت مسبقاً."
                    );
                    return;
                }

                if (_game.Players.Count < 2)
                {
                    await Reply(
                        "❌ لازم لاعبين على الأقل."
                    );
                    return;
                }

                _game.Started = true;
                _game.CurrentPlayerIndex = 0;
                _game.Phase =
                    GamePhase.ChooseOpponent;

                await Reply(
                    "🎈🔥 بدأت لعبة البالونات! 🔥🎈\n\n" +
                    BuildPlayersList() +
                    "\n\n🎯 الدور على: " +
                    _game.CurrentPlayer.Name +
                    "\n\n" +
                    "أرسل رقم اللاعب الذي تريد مهاجمته."
                );
            }

            [Command("بالونات انهاء")]
            public async Task EndGame(string message)
            {
                if (_game == null)
                {
                    await Reply(
                        "❌ لا توجد لعبة."
                    );
                    return;
                }

                _game = null;

                await Reply(
                    "🛑 تم إنهاء لعبة البالونات."
                );
            }

            [Command("بالونات إنهاء")]
            public async Task EndGame2(string message)
            {
                await EndGame(message);
            }

            // =====================================================
            // استقبال الأرقام
            // =====================================================

            public async Task HandleNumber(int number)
            {
                if (_game == null)
                    return;

                if (!_game.Started)
                    return;

                if (_game.GroupId != this.Message.GroupId)
                    return;

                BalloonPlayer current =
                    _game.CurrentPlayer;

                if (current.UserId != this.Message.UserId)
                    return;

                if (_game.Phase ==
                    GamePhase.ChooseOpponent)
                {
                    if (number < 1 ||
                        number > _game.Players.Count)
                    {
                        await Reply(
                            "❌ رقم اللاعب غير صحيح."
                        );
                        return;
                    }

                    BalloonPlayer opponent =
                        _game.Players[number - 1];

                    if (opponent.UserId ==
                        current.UserId)
                    {
                        await Reply(
                            "❌ لا يمكنك اختيار نفسك."
                        );
                        return;
                    }

                    if (opponent.Eliminated)
                    {
                        await Reply(
                            "❌ هذا اللاعب خرج من اللعبة."
                        );
                        return;
                    }

                    _game.SelectedOpponentIndex =
                        number - 1;

                    _game.Phase =
                        GamePhase.ChooseBalloon;

                    await Reply(
                        $"🎯 اخترت {opponent.Name}\n\n" +
                        $"عنده {opponent.Balloons} 🎈\n" +
                        $"أرسل رقم البالونة من 1 إلى {opponent.Balloons}."
                    );

                    return;
                }

                if (_game.Phase ==
                    GamePhase.ChooseBalloon)
                {
                    BalloonPlayer opponent =
                        _game.Players[
                            _game.SelectedOpponentIndex
                        ];

                    if (number < 1 ||
                        number > opponent.Balloons)
                    {
                        await Reply(
                            $"❌ أرسل رقم من 1 إلى {opponent.Balloons}."
                        );
                        return;
                    }

                    await PopBalloon(
                        current,
                        opponent,
                        number
                    );
                }
            }

            private async Task PopBalloon(
                BalloonPlayer current,
                BalloonPlayer opponent,
                int balloon)
            {
                Random random =
                    new Random();

                int chance =
                    random.Next(1, 101);

                // 15% حظ
                if (chance <= 15)
                {
                    await Reply(
                        $"🍀 حظ!\n" +
                        $"{current.Name} اختار البالونة رقم {balloon} " +
                        "لكنها ما انفجرت!\n\n" +
                        "🔄 الدور ينتقل."
                    );

                    NextTurn();
                    await ShowTurn();
                    return;
                }

                // 15% نجاة
                if (chance <= 30)
                {
                    await Reply(
                        $"🛡️ نجاة!\n" +
                        $"البالونة رقم {balloon} بقيت!\n\n" +
                        "🔄 الدور ينتقل."
                    );

                    NextTurn();
                    await ShowTurn();
                    return;
                }

                // فرقعة
                opponent.Balloons--;

                // 10% دور إضافي
                if (chance <= 40)
                {
                    await Reply(
                        $"🔄 دور إضافي!\n\n" +
                        $"💥 انفجرت البالونة رقم {balloon}!\n" +
                        $"{opponent.Name}: {opponent.Balloons} 🎈"
                    );

                    if (opponent.Balloons <= 0)
                    {
                        await Eliminate(opponent);
                        return;
                    }

                    _game!.Phase =
                        GamePhase.ChooseOpponent;

                    await ShowTurn();
                    return;
                }

                // 60% عادي
                await Reply(
                    $"💥 انفجرت البالونة رقم {balloon}!\n" +
                    $"{opponent.Name}: {opponent.Balloons} 🎈"
                );

                if (opponent.Balloons <= 0)
                {
                    await Eliminate(opponent);
                    return;
                }

                NextTurn();
                _game!.Phase =
                    GamePhase.ChooseOpponent;

                await ShowTurn();
            }

            private async Task Eliminate(
                BalloonPlayer player)
            {
                if (_game == null)
                    return;

                player.Eliminated = true;

                await Reply(
                    $"💥 {player.Name} انتهت بالوناته!\n" +
                    "❌ خرج من اللعبة."
                );

                int alive =
                    _game.Players.Count(
                        x => !x.Eliminated
                    );

                if (alive <= 1)
                {
                    BalloonPlayer winner =
                        _game.Players.First(
                            x => !x.Eliminated
                        );

                    await Reply(
                        "🏆🎈 انتهت اللعبة! 🎈🏆\n\n" +
                        $"👑 الفائز: {winner.Name}\n" +
                        "🎉 مبروك!"
                    );

                    _game = null;
                    return;
                }

                if (_game.CurrentPlayer.Eliminated)
                    NextTurn();

                _game.Phase =
                    GamePhase.ChooseOpponent;

                await ShowTurn();
            }

            private void NextTurn()
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

            private async Task ShowTurn()
            {
                if (_game == null)
                    return;

                await Reply(
                    $"🎯 الدور الآن: {_game.CurrentPlayer.Name}\n\n" +
                    "أرسل رقم الخصم."
                );
            }

            private string BuildPlayersList()
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
                    var p =
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
}
