using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.KnowledgeChecks;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Pages.KnowledgeChecks;

public partial class Edit
{
    [Parameter] public Guid? Id { get; set; }
    [SupplyParameterFromQuery(Name = "hold")] public Guid? RequestedHoldId { get; set; }

    private readonly List<KnowledgeCheckQuestion> _questions = [];
    private KnowledgeCheckHold[] _holds = [];
    private KnowledgeCheckPublicationView[] _publications = [];
    private Guid? _loadedRoute;
    private Guid? _draftId;
    private Guid? _holdId;
    private Guid? _confirmClose;
    private KnowledgeCheckPublish? _pendingPublication;
    private string _title = "";
    private string _description = "";
    private string _topic = "";
    private string _feedbackMode = "after-submit";
    private int? _minutes;
    private int? _maxAttempts = 1;
    private long? _revision;
    private string? _error;
    private string? _status;
    private bool _busy;
    private bool _loading;
    private bool _ready;
    private bool _preview;
    private int _previewIndex;

    private bool CanManage => !ViewAs.IsActive && Redirect.CurrentAppRole() == AppRole.Underviser;

    protected override Task OnParametersSetAsync() => !_ready || _loadedRoute != Id ? LoadAsync() : Task.CompletedTask;

    private async Task LoadAsync()
    {
        if (!CanManage) return;
        _loading = true;
        _ready = false;
        _error = null;
        _status = null;
        var requested = Id;
        try
        {
            var indexTask = Client.GetAsync<KnowledgeCheckIndex>();
            var draftTask = requested is { } id ? LoadDraftAsync(id) : Task.FromResult<KnowledgeCheckDraftView?>(null);
            await Task.WhenAll(indexTask, draftTask);
            if (requested != Id) return;

            var index = await indexTask;
            var draft = await draftTask;
            _loadedRoute = requested;
            _holds = index.Holds;
            _holdId = _holds.Any(hold => hold.Id == RequestedHoldId) ? RequestedHoldId : null;
            _title = draft?.Title ?? "";
            _description = draft?.Description ?? "";
            _topic = draft?.Topic ?? "";
            _minutes = draft?.EstimatedMinutes;
            _preview = false;
            _previewIndex = 0;
            _draftId = draft?.Id;
            _revision = draft?.Revision;
            _pendingPublication = null;
            _questions.Clear();
            _questions.AddRange(draft?.Questions ?? [NewQuestion()]);
            _publications = index.Publications.Where(item => item.CanManage && item.QuizId == _draftId).ToArray();
            _ready = true;
        }
        catch (KnowledgeCheckException ex) { if (requested == Id) _error = ex.Message; }
        catch (HttpRequestException) { if (requested == Id) _error = "Kladden kunne ikke hentes. Prøv igen."; }
        finally { if (requested == Id) _loading = false; }
    }

    private async Task<KnowledgeCheckDraftView?> LoadDraftAsync(Guid id) => await Client.GetAsync<KnowledgeCheckDraftView>(id.ToString());

    private async Task SaveAsync(bool publish)
    {
        if (_busy) return;
        _busy = true;
        _error = null;
        _status = null;
        try
        {
            var payload = new KnowledgeCheckDraft(_title, _description, _topic, _minutes, _questions.ToArray(), _revision);
            var draft = _draftId is { } id
                ? await Client.SendAsync<KnowledgeCheckDraftView>(HttpMethod.Put, id.ToString(), payload)
                : await Client.SendAsync<KnowledgeCheckDraftView>(HttpMethod.Post, "", payload);
            _draftId = draft.Id;
            _revision = draft.Revision;
            _status = "Kladden er gemt.";

            if (publish)
            {
                var options = _holdId is { } hold
                    ? new KnowledgeCheckPublish(hold, _maxAttempts, _feedbackMode, draft.Revision)
                    : new KnowledgeCheckPublish(null, null, "after-submit", draft.Revision);
                var requestId = _pendingPublication is { } pending && pending with { RequestId = null } == options
                    ? pending.RequestId
                    : Guid.NewGuid();
                _pendingPublication = options = options with { RequestId = requestId };
                var result = await Client.SendAsync<KnowledgeCheckPublicationView>(HttpMethod.Post, $"{draft.Id}/publish", options);
                _publications = [result, .. _publications.Where(item => item.Id != result.Id)];
                _pendingPublication = null;
                _status = "Videnstjekket er udgivet.";
            }
            if (Id is null)
            {
                _loadedRoute = draft.Id;
                Nav.NavigateTo($"/videnstjek/rediger/{draft.Id}", replace: true);
            }
        }
        catch (KnowledgeCheckException ex) { _error = ex.Message; }
        catch (HttpRequestException) { _error = "Ændringen kunne ikke gemmes. Prøv igen."; }
        finally { _busy = false; }
    }

    private async Task CloseAsync(Guid id)
    {
        if (_busy) return;
        _busy = true;
        _error = null;
        _status = null;
        try
        {
            var result = await Client.SendAsync<KnowledgeCheckPublicationView>(HttpMethod.Post, $"publications/{id}/close");
            _publications = _publications.Select(item => item.Id == result.Id ? result : item).ToArray();
            _confirmClose = null;
            _status = "Udgivelsen er lukket.";
        }
        catch (KnowledgeCheckException ex) { _error = ex.Message; }
        catch (HttpRequestException) { _error = "Udgivelsen kunne ikke lukkes. Prøv igen."; }
        finally { _busy = false; }
    }

    private static KnowledgeCheckQuestion NewQuestion() => new(Guid.NewGuid(), "single", "", ["", ""], [], [], false, "", null);

    private void AddQuestion() => _questions.Add(NewQuestion());

    private void TogglePreview()
    {
        _preview = !_preview;
        _previewIndex = 0;
    }

    private void Remove(int index)
    {
        _questions.RemoveAt(index);
        _previewIndex = Math.Clamp(_previewIndex, 0, Math.Max(0, _questions.Count - 1));
    }

    private void Move(int index, int direction)
    {
        var destination = index + direction;
        if (destination < 0 || destination >= _questions.Count) return;
        (_questions[index], _questions[destination]) = (_questions[destination], _questions[index]);
        _previewIndex = Math.Clamp(_previewIndex, 0, _questions.Count - 1);
    }

    private void Update(int index, Func<KnowledgeCheckQuestion, KnowledgeCheckQuestion> apply) =>
        _questions[index] = apply(_questions[index]);

    private void ChangeKind(int index, string kind) => Update(index, question =>
    {
        if (kind is "single" or "multiple" && question.Kind is "single" or "multiple")
            return question with { Kind = kind, CorrectOptions = kind == "single" ? question.CorrectOptions.Take(1).ToArray() : question.CorrectOptions };
        return question with
        {
            Kind = kind,
            Options = kind switch { "boolean" => ["Sandt", "Falsk"], "short" => [], _ => ["", ""] },
            CorrectOptions = [],
            AcceptedAnswers = [],
            ManualGrading = false,
            OptionExplanations = null
        };
    });

    private void ChangeOption(int index, int choice, string text) => Update(index, question =>
    {
        var options = (string[])question.Options.Clone();
        options[choice] = text;
        return question with { Options = options };
    });

    private void ChangeOptionExplanation(int index, int choice, string text) => Update(index, question =>
    {
        var explanations = question.OptionExplanations?.ToArray() ?? Enumerable.Repeat("", question.Options.Length).ToArray();
        explanations[choice] = text;
        return question with { OptionExplanations = explanations };
    });

    private void AddOption(int index) => Update(index, question => question with
    {
        Options = [.. question.Options, ""],
        OptionExplanations = question.OptionExplanations is { } explanations ? [.. explanations, ""] : null
    });

    private void RemoveOption(int index, int choice) => Update(index, question => question with
    {
        Options = question.Options.Where((_, position) => position != choice).ToArray(),
        OptionExplanations = question.OptionExplanations?.Where((_, position) => position != choice).ToArray(),
        CorrectOptions = question.CorrectOptions.Where(position => position != choice)
            .Select(position => position > choice ? position - 1 : position).ToArray()
    });

    private void ChangeCorrect(int index, int choice, bool selected) => Update(index, question => question with
    {
        CorrectOptions = question.Kind != "multiple"
            ? [choice]
            : selected ? [.. question.CorrectOptions.Append(choice).Distinct().Order()] : question.CorrectOptions.Where(value => value != choice).ToArray()
    });

    private static string Text(ChangeEventArgs args) => args.Value?.ToString() ?? "";
}
