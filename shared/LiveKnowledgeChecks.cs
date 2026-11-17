namespace Mercantec.Tech.Contracts;

public static class LiveQuiz
{
    public const string Lobby = "lobby";
    public const string Question = "question";
    public const string Reveal = "reveal";
    public const string Leaderboard = "leaderboard";
    public const string Finished = "finished";
    public const string PlayerHeader = "X-Lup-Live-Player";

    public static bool IsCode(string? code) => code is { Length: 6 } && code.All(char.IsAsciiDigit);
}

public sealed record LiveQuizCreate(Guid SourceId, bool FromDraft, int QuestionSeconds = 20);
public sealed record LiveQuizJoin(string Nickname, string? PlayerToken = null);
public sealed record LiveQuizJoined(string PlayerToken, LiveQuizState State);
public sealed record LiveQuizAnswer(int QuestionIndex, int[] SelectedOptions, string? Text = null);
public sealed record LiveQuizAdvance(string Phase, int QuestionIndex);
public sealed record LiveQuizLock(bool Locked);
public sealed record LiveQuizKick(Guid PlayerId);

public sealed record LiveQuizQuestion(string Kind, string Prompt, string[] Options);
public sealed record LiveQuizReveal(int[] CorrectOptions, string[] AcceptedAnswers, int[] AnswerCounts,
    string Explanation, string? LearnMoreUrl, string? LearnMoreTitle);
public sealed record LiveQuizPlayer(Guid Id, string Nickname);
public sealed record LiveQuizStanding(int Rank, string Nickname, int Score);
public sealed record LiveQuizPersonal(string Nickname, int Score, int Rank, bool HasAnswered,
    int[] SelectedOptions, string? Text, bool? Correct, int? Points, int Streak);

public sealed record LiveQuizState(
    string Code, string Title, string Phase, int QuestionIndex, int QuestionCount, int QuestionSeconds,
    DateTimeOffset ServerNowUtc, DateTimeOffset? DeadlineUtc, bool Locked, bool IsHost,
    int PlayerCount, int AnsweredCount, LiveQuizQuestion? Question, LiveQuizReveal? Reveal,
    LiveQuizPlayer[] Players, LiveQuizStanding[] Standings, LiveQuizPersonal? Personal);
