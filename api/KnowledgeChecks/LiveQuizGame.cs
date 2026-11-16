using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Api.KnowledgeChecks;

internal sealed record LiveQuizGame(string Phase, int QuestionIndex, bool Locked, DateTimeOffset? DeadlineUtc,
    LiveQuizParticipant[] Players, LiveQuizRoundAnswer[] Answers)
{
    public static LiveQuizGame New() => new(LiveQuiz.Lobby, -1, false, null, [], []);

    public string PhaseAt(DateTimeOffset now) => Phase == LiveQuiz.Question
        && (now >= DeadlineUtc || Players.Length > 0 && Answers.Length == Players.Length)
            ? LiveQuiz.Reveal : Phase;

    public LiveQuizGame OpenQuestion(DateTimeOffset now, int seconds) => this with
    {
        Phase = LiveQuiz.Question, QuestionIndex = QuestionIndex + 1,
        DeadlineUtc = now.AddSeconds(seconds), Answers = []
    };

    public LiveQuizGame ShowLeaderboard() => this with
    {
        Phase = LiveQuiz.Leaderboard, Players = ScorePlayers()
    };

    public LiveQuizGame Finish() => this with
    {
        Phase = LiveQuiz.Finished, Locked = true,
        Players = Phase is LiveQuiz.Question or LiveQuiz.Reveal ? ScorePlayers() : Players
    };

    public LiveQuizState Project(LiveKnowledgeCheck room, KnowledgeCheckQuestion[] questions, DateTimeOffset now,
        bool isHost, LiveQuizParticipant? player = null)
    {
        var phase = PhaseAt(now);
        var showQuestion = phase is LiveQuiz.Question or LiveQuiz.Reveal;
        var question = showQuestion ? questions[QuestionIndex] : null;
        var rankings = Rankings(phase == LiveQuiz.Reveal ? ScorePlayers() : Players);
        var personal = player is null ? null : PersonalState(player.Id, phase, rankings);
        return new(room.Code, room.Title, phase, QuestionIndex, questions.Length, room.QuestionSeconds, now,
            phase == LiveQuiz.Question ? DeadlineUtc : null, Locked, isHost, Players.Length, Answers.Length,
            question is null ? null : new(question.Kind, question.Prompt, question.Options),
            phase == LiveQuiz.Reveal ? Reveal(question!) : null,
            isHost && phase == LiveQuiz.Lobby ? Players.Select(p => new LiveQuizPlayer(p.Id, p.Nickname)).ToArray() : [],
            phase is LiveQuiz.Leaderboard or LiveQuiz.Finished && QuestionIndex >= 0
                ? rankings.Take(5).Select(entry => new LiveQuizStanding(entry.Rank, entry.Player.Nickname, entry.Player.Score)).ToArray()
                : [],
            personal);
    }

    public static int Points(DateTimeOffset deadline, int seconds, DateTimeOffset received) =>
        500 + (int)Math.Round(500 * Math.Clamp((deadline - received).TotalSeconds / seconds, 0, 1));

    private LiveQuizReveal Reveal(KnowledgeCheckQuestion question)
    {
        var counts = new int[question.Options.Length];
        foreach (var answer in Answers)
            foreach (var index in answer.SelectedOptions)
                counts[index]++;
        return new(question.CorrectOptions, question.AcceptedAnswers, counts, question.Explanation,
            question.LearnMoreUrl, question.LearnMoreTitle);
    }

    private LiveQuizPersonal PersonalState(Guid playerId, string phase,
        (LiveQuizParticipant Player, int Rank)[] rankings)
    {
        var (player, rank) = rankings.Single(entry => entry.Player.Id == playerId);
        var answer = Answers.FirstOrDefault(item => item.PlayerId == playerId);
        var showResult = phase is LiveQuiz.Reveal or LiveQuiz.Leaderboard or LiveQuiz.Finished && QuestionIndex >= 0;
        return new(player.Nickname, player.Score, rank, answer is not null, answer?.SelectedOptions ?? [], answer?.Text,
            showResult ? answer?.Correct ?? false : null, showResult ? answer?.Points ?? 0 : null, player.Streak);
    }

    private LiveQuizParticipant[] ScorePlayers()
    {
        var answers = Answers.ToDictionary(answer => answer.PlayerId);
        return Players.Select(player => answers.TryGetValue(player.Id, out var answer) && answer.Correct
            ? player with { Score = player.Score + answer.Points, Streak = player.Streak + 1 }
            : player with { Streak = 0 }).ToArray();
    }

    private static (LiveQuizParticipant Player, int Rank)[] Rankings(LiveQuizParticipant[] players)
    {
        var ordered = players.OrderByDescending(player => player.Score).ToArray();
        var rankings = new (LiveQuizParticipant Player, int Rank)[ordered.Length];
        for (var index = 0; index < ordered.Length; index++)
        {
            var rank = index > 0 && ordered[index].Score == ordered[index - 1].Score
                ? rankings[index - 1].Rank : index + 1;
            rankings[index] = (ordered[index], rank);
        }
        return rankings;
    }
}

internal sealed record LiveQuizParticipant(Guid Id, string Nickname, string TokenHash, int Score = 0, int Streak = 0);
internal sealed record LiveQuizRoundAnswer(Guid PlayerId, int[] SelectedOptions, string? Text, bool Correct, int Points);
