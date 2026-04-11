using Microsoft.AspNetCore.Components;
using TifSnippetApp.Client.Models;

namespace TifSnippetApp.Client.Components
{
    public partial class SnippetEntry : IDisposable
    {
        [Inject] public HttpClient Http { get; set; } = null!;

        [Parameter] public SnippetInfo SnippetData { get; set; } = null!;
        [Parameter] public bool IsActive { get; set; }
        [Parameter] public EventCallback OnAccepted { get; set; }
        [Parameter] public EventCallback OnBlanked { get; set; }
        [Parameter] public EventCallback OnFocused { get; set; }
 
        public ElementReference RowElement { get; private set; }
        private ElementReference _inputElement;
        public string EntryValue { get; set; } = string.Empty;
        private string _originalValue = string.Empty;
        public SnippetStatus ReviewStatus { get; set; } = SnippetStatus.None;
        public bool IsEdited => (EntryValue?.Trim() ?? "") != (_originalValue?.Trim() ?? "");

        protected override void OnInitialized()
        {
            if (SnippetData != null)
            {
                EntryValue = SnippetData.Content;
                _originalValue = SnippetData.Content;
            }
        }

        private void OnInputChanged(ChangeEventArgs e)
        {
            EntryValue = e.Value?.ToString() ?? string.Empty;
            StateHasChanged();
        }

        public void Collapse() { /* No longer needed but referenced in parent? */ }

        public async Task FocusEdit() => await _inputElement.FocusAsync();
        public async Task Accept()    => await OnAccepted.InvokeAsync();
        public async Task Blank()
        {
            EntryValue = string.Empty;
            await OnBlanked.InvokeAsync();
        }

        private async Task HandleRowClicked()
        {
            await OnFocused.InvokeAsync();
            await FocusEdit();
        }

        private async Task HandleInputFocused() => await OnFocused.InvokeAsync();

        // ── Status helpers ────────────────────────────────────────────────────

        private string GetStatusClass() => ReviewStatus switch
        {
            SnippetStatus.Accepted => "status-accepted",
            SnippetStatus.Edited   => "status-edited",
            SnippetStatus.RejectedBlank or SnippetStatus.RejectedIllegible or SnippetStatus.RejectedOther => "status-rejected",
            _ => string.Empty
        };

        private string GetBadgeClass() => ReviewStatus switch
        {
            SnippetStatus.Accepted => "bg-success",
            SnippetStatus.Edited   => "bg-info text-dark",
            SnippetStatus.RejectedBlank or SnippetStatus.RejectedIllegible or SnippetStatus.RejectedOther => "bg-secondary",
            _ => string.Empty
        };

        private string GetStatusText() => ReviewStatus switch
        {
            SnippetStatus.Accepted          => "Accepted",
            SnippetStatus.Edited            => "Edited",
            SnippetStatus.RejectedBlank     => "Rejected (Blank)",
            SnippetStatus.RejectedIllegible => "Rejected (Illegible)",
            SnippetStatus.RejectedOther     => "Rejected (Other)",
            _ => string.Empty
        };

        public void Dispose()
        {
        }
    }
}
