using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WolfLive.Api;
using WolfLive.Api.Commands;
using WolfLive.Api.Models;

public class Program
{
    private static IWolfClient _client = null!;

    internal static BalloonGame? Game;

    public static async Task Main(string[] args)
    {
        string email =
            Environment.GetEnvironmentVariable("WOLF_EMAIL") ?? "";

        string password =
            Environment.GetEnvironmentVariable("WOLF_PASSWORD") ?? "";

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine(
                "❌ WOLF_EMAIL أو WOLF_PASSWORD غير موجود.");
            return;
        }

        _client = new WolfClient()
            .SetupCommands()
            .WithCommandSet(c =>
            {
                c.AddCommands<BalloonCommands>()
                 .WithPrefix("!");
            })
            .WithSerilog()
            .Done();

        _client.OnConnected += (_) =>
        {
            Console.WriteLine(
                "✅ Connected to wolf.live!");
        };

        _client.Messaging.OnMessage += async (client, message) =>
        {
            try
            {
                string text =
                    message.Content?.Trim() ?? "";

                Console.WriteLine(
                    $"📩 Message: [{text}] " +
                    $"User={message.UserId} " +
                    $"Group={message.GroupId}");

                if (int.TryParse(text, out int number))
                {
                    await HandleNumber(
                        client,
                        message,
                        number);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "❌ Message error: " +
                    ex.Message);
            }
        };

        bool result =
            await _client.Login(
                email,
                password);

        Console.WriteLine(
            $"Login {(result ? "success!" : "failed!")}");

        if (!result)
            return;

        await Task.Delay(-1);
    }

    private static async Task HandleNumber(
        IWolfClient client,
        Message message,
        int number)
    {
        if (Game == null ||
            !Game.Started)
            return;

        if (!string.IsNullOrEmpty(Game.GroupId) &&
            message.GroupId != Game.GroupId)
            return;

        if (number < 1)
            return;

        BalloonPlayer? player =
            Game.GetPlayer(message.UserId);

        if (player == null)
        {
            await message.Reply(
                client,
                "❌ أنت مو مشترك باللعبة.\n" +
                "اكتب: !بالونات انضم");
            return;
        }

        if (player.Eliminated)
        {
            await message.Reply(
                client,
                "❌ أنت خارج اللعبة.");
            return;
        }

        if (Game.CurrentPlayerId != player.UserId)
            return;

        // =========================
        // اختيار الخصم
        // =========================

        if (Game.WaitingForOpponent)
        {
            if (number > Game.AlivePlayers.Count)
            {
                await message.Reply(
                    client,
                    $"❌ اختار رقم لاعب من 1 إلى " +
                    $"{Game.AlivePlayers.Count}.");
                return;
            }

            BalloonPlayer? opponent =
                Game.GetPlayerByNumber(number);

            if (opponent == null ||
                opponent.Eliminated)
            {
                await message.Reply(
                    client,
                    "❌ هذا اللاعب غير موجود.");
                return;
            }

            if (opponent.UserId == player.UserId)
            {
                await message.Reply(
                    client,
                    "❌ ما تگدر تختار نفسك.");
                return;
            }

            Game.SelectedOpponentId =
                opponent.UserId;

            Game.WaitingForOpponent = false;
            Game.WaitingForBalloon = true;

            await message.Reply(
                client,
                $"🎯 اخترت: {opponent.Name}\n\n" +
                $"🎈 عنده {opponent.Balloons} بالونات.\n" +
                $"اختار رقم البالون من 1 إلى " +
                $"{opponent.Balloons}.");

            return;
        }

        // =========================
        // اختيار البالون
        // =========================

        if (Game.WaitingForBalloon)
        {
            if (Game.SelectedOpponentId == null)
                return;

            BalloonPlayer? opponent =
                Game.GetPlayer(
                    Game.SelectedOpponentId);

            if (opponent == null ||
                opponent.Eliminated)
            {
                Game.ResetTurnSelection();

                await message.Reply(
                    client,
                    "❌ الخصم لم يعد متاحاً.");
                return;
            }

            if (number > opponent.Balloons)
            {
                await message.Reply(
                    client,
                    $"❌ اختار رقم من 1 إلى " +
                    $"{opponent.Balloons}.");
                return;
            }

            await ResolveBalloon(
                client,
                message,
                player,
                opponent,
                number);
        }
    }

    private static async Task ResolveBalloon(
        IWolfClient client,
        Message message,
        BalloonPlayer attacker,
        BalloonPlayer opponent,
        int balloonNumber)
    {
        Game!.ResetTurnSelection();

        int roll =
            Random.Shared.Next(1, 101);

        // =========================
        // 15% حظ
        // =========================

        if (roll <= 15)
        {
            await message.Reply(
                client,
                $"🍀 حظ!\n\n" +
                $"🎈 البالون رقم {balloonNumber} " +
                $"ما انفجر!\n" +
                $"😎 {opponent.Name} نجا.\n\n" +
                $"➡️ الدور ينتقل.");

            Game.NextTurn();

            await SendTurnMessage(
                client,
                message);

            return;
        }

        // =========================
        // 15% نجاة
        // =========================

        if (roll <= 30)
        {
            await message.Reply(
                client,
                $"🛡️ نجاة!\n\n" +
                $"🎈 البالون رقم {balloonNumber} " +
                $"بقي مكانه.\n" +
                $"👏 {opponent.Name} نجا.\n\n" +
                $"➡️ الدور ينتقل.");

            Game.NextTurn();

            await SendTurnMessage(
                client,
                message);

            return;
        }

        // =========================
        // 10% دور إضافي
        // =========================

        if (roll <= 40)
        {
            opponent.Balloons--;

            if (opponent.Balloons < 0)
                opponent.Balloons = 0;

            if (opponent.Balloons == 0)
            {
                opponent.Eliminated = true;

                await message.Reply(
                    client,
                    $"💥🎈 انفجار!\n\n" +
                    $"🎈 البالون رقم " +
                    $"{balloonNumber} انفجر!\n" +
                    $"❌ {opponent.Name} خسر آخر بالون.\n\n" +
                    $"🔥 عندك دور إضافي!");

                if (Game.CheckWinner(
                    out BalloonPlayer? winner))
                {
                    await FinishGame(
                        client,
                        message,
                        winner!);

                    return;
                }

                await SendTurnMessage(
                    client,
                    message);

                return;
            }

            await message.Reply(
                client,
                $"🔄 دور إضافي!\n\n" +
                $"💥 البالون رقم {balloonNumber} " +
                $"انفجر!\n" +
                $"❌ {opponent.Name} خسر بالوناً.\n" +
                $"🎈 المتبقي: {opponent.Balloons}\n\n" +
                $"🔥 {attacker.Name} عنده دور إضافي!");

            await SendTurnMessage(
                client,
                message);

            return;
        }

        // =========================
        // 60% انفجار طبيعي
        // =========================

        opponent.Balloons--;

        if (opponent.Balloons < 0)
            opponent.Balloons = 0;

        if (opponent.Balloons == 0)
        {
            opponent.Eliminated = true;

            await message.Reply(
                client,
                $"💥🎈 انفجر البالون رقم " +
                $"{balloonNumber}!\n\n" +
                $"❌ {opponent.Name} خسر آخر بالون.\n" +
                $"☠️ تم إقصاؤه.");

            if (Game.CheckWinner(
                out BalloonPlayer? winner))
            {
                await FinishGame(
                    client,
                    message,
                    winner!);

                return;
            }

            Game.NextTurn();

            await SendTurnMessage(
                client,
                message);

            return;
        }

        await message.Reply(
            client,
            $"💥🎈 انفجر البالون رقم " +
            $"{balloonNumber}!\n\n" +
            $"❌ {opponent.Name} خسر بالوناً.\n" +
            $"🎈 المتبقي: {opponent.Balloons}\n\n" +
            $"➡️ الدور ينتقل.");

        Game.NextTurn();

        await SendTurnMessage(
            client,
            message);
    }

    // =========================
    // رسالة الدور الجديد
    // =========================

    private static async Task SendTurnMessage(
        IWolfClient client,
        Message message)
    {
        if (Game == null ||
            !Game.Started)
            return;

        BalloonPlayer? current =
            Game.GetCurrentPlayer();

        if (current == null)
            return;

        await message.Reply(
            client,
            $"🎯 الدور الآن على: {current.Name}\n\n" +
            Game.GetPlayersText() +
            "\n\n" +
            $"👤 {current.Name} اختار رقم الخصم.");
    }

    // =========================
    // نهاية اللعبة
    // =========================

    private static async Task FinishGame(
        IWolfClient client,
        Message message,
        BalloonPlayer winner)
    {
        await message.Reply(
            client,
            $"🏆🎉🎉 انتهت لعبة البالونات! 🎉🎉🏆\n\n" +
            $"🥇 الفائز: {winner.Name}\n" +
            $"🎈 البالونات المتبقية: {winner.Balloons}\n\n" +
            $"🔥 مبروك!");

        Game!.Started = false;
    }
}

// ==================================================
// أوامر لعبة البالونات
// ==================================================

public class BalloonCommands : WolfContext
{
    [Command("بالونات")]
    public async Task Help(string message)
    {
        await this.Reply(
            "🎈🔥 لعبة البالونات 🔥🎈\n\n" +
            "الأوامر:\n\n" +
            "🎮 !بالونات جديد\n" +
            "👤 !بالونات انضم\n" +
            "📋 !بالونات لاعبين\n" +
            "▶️ !بالونات بدء\n" +
            "🛑 !بالونات انهاء\n\n" +
            "🎈 كل لاعب يبدأ بـ 7 بالونات.\n" +
            "🎯 بعد بدء اللعبة تختار رقم الخصم.\n" +
            "🎈 بعدها تختار رقم البالون.\n\n" +
            "🍀 15% حظ\n" +
            "🛡️ 15% نجاة\n" +
            "🔄 10% دور إضافي\n" +
            "💥 60% انفجار طبيعي");
    }

    [Command("بالونات مساعدة")]
    public async Task Help2(string message)
    {
        await Help(message);
    }

    // =========================
    // إنشاء لعبة
    // =========================

    [Command("بالونات جديد")]
    public async Task NewGame(string message)
    {
        if (Program.Game != null &&
            Program.Game.Started)
        {
            await this.Reply(
                "❌ توجد لعبة قيد التشغيل.");
            return;
        }

        Program.Game =
            new BalloonGame();

        await this.Reply(
            "🎈🔥 تم إنشاء لعبة البالونات! 🔥🎈\n\n" +
            "كل لاعب يبدأ بـ 7 🎈\n\n" +
            "للانضمام:\n" +
            "!بالونات انضم\n\n" +
            "بعدها:\n" +
            "!بالونات بدء");
    }

    // =========================
    // انضمام
    // =========================

    [Command("بالونات انضم")]
    public async Task Join(string message)
    {
        if (Program.Game == null)
        {
            await this.Reply(
                "❌ ماكو لعبة.\n" +
                "اكتب !بالونات جديد");
            return;
        }

        if (Program.Game.Started)
        {
            await this.Reply(
                "❌ اللعبة بدأت بالفعل.");
            return;
        }

        string userId =
            Message.UserId;

        string groupId =
            Message.GroupId ?? "";

        string result =
            Program.Game.AddPlayer(
                userId,
                groupId);

        await this.Reply(result);
    }

    [Command("بالونات انضمام")]
    public async Task Join2(string message)
    {
        await Join(message);
    }

    // =========================
    // عرض اللاعبين
    // =========================

    [Command("بالونات لاعبين")]
    public async Task Players(string message)
    {
        if (Program.Game == null)
        {
            await this.Reply(
                "❌ ماكو لعبة حالياً.");
            return;
        }

        await this.Reply(
            Program.Game.GetPlayersText());
    }

    // =========================
    // بدء اللعبة
    // =========================

    [Command("بالونات بدء")]
    public async Task Start(string message)
    {
        if (Program.Game == null)
        {
            await this.Reply(
                "❌ ماكو لعبة.\n" +
                "اكتب !بالونات جديد");
            return;
        }

        if (Program.Game.Started)
        {
            await this.Reply(
                "❌ اللعبة بدأت بالفعل.");
            return;
        }

        if (Program.Game.Players.Count < 2)
        {
            await this.Reply(
                "❌ لازم لاعبين اثنين على الأقل.");
            return;
        }

        Program.Game.Started = true;

        Program.Game.CurrentPlayerIndex = 0;

        Program.Game.ResetTurnSelection();

        await this.Reply(
            "🎈🔥🔥 بدأت لعبة البالونات! 🔥🔥🎈\n\n" +
            Program.Game.GetPlayersText() +
            "\n\n" +
            $"🎯 الدور على: " +
            $"{Program.Game.GetCurrentPlayer()?.Name}\n\n" +
            "👤 اختار رقم الخصم.");
    }

    // =========================
    // إنهاء اللعبة
    // =========================

    [Command("بالونات انهاء")]
    public async Task Stop(string message)
    {
        if (Program.Game == null)
        {
            await this.Reply(
                "❌ ماكو لعبة حالياً.");
            return;
        }

        Program.Game = null;

        await this.Reply(
            "🛑 تم إنهاء لعبة البالونات.");
    }

    [Command("بالونات إنهاء")]
    public async Task Stop2(string message)
    {
        await Stop(message);
    }
}

// ==================================================
// نظام اللعبة
// ==================================================

public class BalloonGame
{
    public string GroupId { get; private set; } = "";

    public bool Started { get; set; }

    public int CurrentPlayerIndex { get; set; }

    public bool WaitingForOpponent { get; set; } = true;

    public bool WaitingForBalloon { get; set; }

    public string? SelectedOpponentId { get; set; }

    public List<BalloonPlayer> Players { get; } =
        new List<BalloonPlayer>();

    public List<BalloonPlayer> AlivePlayers =>
        Players
            .Where(p => !p.Eliminated)
            .ToList();

    public string? CurrentPlayerId
    {
        get
        {
            return GetCurrentPlayer()?.UserId;
        }
    }

    // =========================
    // إضافة لاعب
    // =========================

    public string AddPlayer(
        string userId,
        string groupId)
    {
        if (Players.Any(
            p => p.UserId == userId))
        {
            return "❌ أنت مشترك بالفعل.";
        }

        if (Players.Count >= 50)
        {
            return
                "❌ وصلت اللعبة للحد الأقصى " +
                "50 لاعب.";
        }

        if (Players.Count == 0)
        {
            GroupId = groupId;
        }

        if (GroupId != groupId)
        {
            return
                "❌ هذه اللعبة مرتبطة بروم آخر.";
        }

        string name =
            $"لاعب {Players.Count + 1}";

        Players.Add(
            new BalloonPlayer(
                userId,
                name));

        return
            $"✅ تم انضمامك!\n\n" +
            $"👤 {name}\n" +
            $"🎈 7 بالونات\n\n" +
            GetPlayersText();
    }

    // =========================
    // الحصول على لاعب
    // =========================

    public BalloonPlayer? GetPlayer(
        string userId)
    {
        return Players.FirstOrDefault(
            p => p.UserId == userId);
    }

    // =========================
    // اللاعب حسب الرقم
    // =========================

    public BalloonPlayer? GetPlayerByNumber(
        int number)
    {
        if (number < 1 ||
            number > AlivePlayers.Count)
            return null;

        return AlivePlayers[number - 1];
    }

    // =========================
    // اللاعب الحالي
    // =========================

    public BalloonPlayer? GetCurrentPlayer()
    {
        var alive =
            AlivePlayers;

        if (alive.Count == 0)
            return null;

        if (CurrentPlayerIndex >=
            alive.Count)
        {
            CurrentPlayerIndex = 0;
        }

        return alive[
            CurrentPlayerIndex];
    }

    // =========================
    // الانتقال للدور التالي
    // =========================

    public void NextTurn()
    {
        var alive =
            AlivePlayers;

        if (alive.Count == 0)
            return;

        CurrentPlayerIndex++;

        if (CurrentPlayerIndex >=
            alive.Count)
        {
            CurrentPlayerIndex = 0;
        }

        ResetTurnSelection();
    }

    // =========================
    // إعادة اختيار الدور
    // =========================

    public void ResetTurnSelection()
    {
        WaitingForOpponent = true;

        WaitingForBalloon = false;

        SelectedOpponentId = null;
    }

    // =========================
    // فحص الفائز
    // =========================

    public bool CheckWinner(
        out BalloonPlayer? winner)
    {
        var alive =
            AlivePlayers;

        if (alive.Count == 1)
        {
            winner = alive[0];
            return true;
        }

        winner = null;

        return false;
    }

    // =========================
    // عرض اللاعبين
    // =========================

    public string GetPlayersText()
    {
        if (Players.Count == 0)
        {
            return "👥 لا يوجد لاعبين.";
        }

        var alive =
            AlivePlayers;

        var lines =
            new List<string>();

        for (int i = 0;
             i < alive.Count;
             i++)
        {
            BalloonPlayer player =
                alive[i];

            string balloons =
                string.Concat(
                    Enumerable.Repeat(
                        "🎈",
                        Math.Min(
                            player.Balloons,
                            7)));

            lines.Add(
                $"{NumberEmoji(i + 1)} " +
                $"{player.Name} — " +
                $"{player.Balloons} {balloons}");
        }

        var eliminated =
            Players
                .Where(p => p.Eliminated)
                .ToList();

        if (eliminated.Count > 0)
        {
            lines.Add("");

            lines.Add(
                "☠️ المقصيون:");

            foreach (
                BalloonPlayer player
                in eliminated)
            {
                lines.Add(
                    $"❌ {player.Name} — 0 🎈");
            }
        }

        return string.Join(
            "\n",
            lines);
    }

    // =========================
    // أرقام اللاعبين
    // =========================

    private static string NumberEmoji(
        int number)
    {
        return number switch
        {
            1 => "1️⃣",
            2 => "2️⃣",
            3 => "3️⃣",
            4 => "4️⃣",
            5 => "5️⃣",
            6 => "6️⃣",
            7 => "7️⃣",
            8 => "8️⃣",
            9 => "9️⃣",
            10 => "🔟",
            _ => $"{number}."
        };
    }
}

// ==================================================
// بيانات اللاعب
// ==================================================

public class BalloonPlayer
{
    public string UserId { get; }

    public string Name { get; }

    public int Balloons { get; set; } = 7;

    public bool Eliminated { get; set; }

    public BalloonPlayer(
        string userId,
        string name)
    {
        UserId = userId;

        Name = name;
    }
}
