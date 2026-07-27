// Blazor JS initializer (auto-discovered by assembly name).
//
// Enhanced navigation preserves the document and DOM-diffs each fetched page
// against it. The server markup carries no data-bs-theme attribute, so the diff
// strips the one set by the auto-dark script in App.razor, flashing the page to
// light until a full refresh. Reapplying the OS-preferred theme on every
// enhancedload keeps the scheme stable across client-side navigation (#35).
export function afterWebStarted(blazor) {
    blazor.addEventListener("enhancedload", () => window.applyPreferredTheme());
}
