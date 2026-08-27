using System;
using System.Collections;
using System.Text;
using UnityEngine;
using TMPro;

/// <summary>
/// The single path every Coach Byte message travels: build prompt -> ask Gemini
/// -> clean up -> enforce the bubble's word limit -> show it -> log it.
///
/// Centralised so all seven Coach Byte moments behave identically. In particular
/// the 14-word cap is enforced HERE, in code, not just requested in the prompt -
/// a model that ignores the instruction must not be able to overflow the chat
/// bubble, so an over-long reply is discarded in favour of a local line rather
/// than truncated into a dangling fragment.
///
/// Never throws into the caller and never blocks gameplay: if the backend is
/// down the fallback line is shown and a warning is logged for developers only.
/// </summary>
public static class CoachByteMessenger
{
    /// <summary>
    /// Generates and displays one message.
    /// </summary>
    /// <param name="host">MonoBehaviour used to run the web request coroutine.</param>
    /// <param name="contextName">One of the CoachBytePromptBuilder constants.</param>
    /// <param name="ctx">Gathered context. May be null - the fallback still works.</param>
    /// <param name="target">Where to write. May be null if onMessage is supplied.</param>
    /// <param name="model">Gemini model id.</param>
    /// <param name="onMessage">Optional extra sink (e.g. a caller that owns its own label).</param>
    public static void Speak(MonoBehaviour host, string contextName, CoachByteContext ctx,
        TMP_Text target, string model, Action<string> onMessage = null)
    {
        if (host == null || !host.isActiveAndEnabled)
        {
            // Nothing to run the coroutine on - still honour the request locally so
            // the bubble is never left showing placeholder text.
            Deliver(contextName, ctx, target, onMessage, CoachBytePromptBuilder.Fallback(contextName, ctx), false);
            return;
        }

        host.StartCoroutine(SpeakRoutine(contextName, ctx, target, model, onMessage));
    }

    private static IEnumerator SpeakRoutine(string contextName, CoachByteContext ctx,
        TMP_Text target, string model, Action<string> onMessage)
    {
        string prompt = CoachBytePromptBuilder.Build(contextName, ctx);
        string accepted = null;

        yield return GeminiClient.Generate(
            model,
            prompt,
            onSuccess: raw =>
            {
                string cleaned = Sanitize(raw);

                if (string.IsNullOrWhiteSpace(cleaned))
                {
                    Debug.LogWarning("[CoachByte] Empty response for " + contextName + " - using local line.");
                    return;
                }

                // The cap is a hard UI constraint, not a preference: the chat bubble
                // fits about 14 words. Discarding is deliberate - a truncated sentence
                // ("That 38-hit combo last time was") reads worse than a short
                // hand-written one that actually finishes.
                int words = CountWords(cleaned);
                if (words > CoachBytePromptBuilder.MaxWords)
                {
                    Debug.LogWarning("[CoachByte] " + contextName + " reply was " + words +
                        " words (limit " + CoachBytePromptBuilder.MaxWords + ") - using local line instead: " + cleaned);
                    return;
                }

                accepted = cleaned;
            },
            onError: err => Debug.LogWarning("[CoachByte] " + contextName + ": " + err));

        bool fromAi = !string.IsNullOrEmpty(accepted);
        string message = fromAi ? accepted : CoachBytePromptBuilder.Fallback(contextName, ctx);

        Deliver(contextName, ctx, target, onMessage, message, fromAi);
    }

    // Only AI-generated lines are logged. Writing fallbacks into the history would
    // poison the anti-repetition context: the model would be told to avoid wording
    // it never produced, while the lines it actually did produce fall out of view.
    private static void Deliver(string contextName, CoachByteContext ctx, TMP_Text target,
        Action<string> onMessage, string message, bool fromAi)
    {
        if (target != null) target.text = message;
        if (onMessage != null) onMessage(message);

        if (!fromAi) return;

        string mode = ctx != null ? (ctx.currentMode ?? ctx.recommendedMode ?? "") : "";
        try
        {
            CoachByteHistory.Append(contextName, mode, message);
        }
        catch (Exception e)
        {
            // History is a nice-to-have; a locked or unwritable file must never
            // take down the menu the message was just displayed on.
            Debug.LogWarning("[CoachByte] Could not write history: " + e.Message);
        }
    }

    /// <summary>
    /// Strips the formatting models add despite being told not to: surrounding
    /// quotes, stray markdown, newlines, and trailing whitespace.
    /// </summary>
    public static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        string s = raw.Replace("\r", " ").Replace("\n", " ").Replace("*", "").Replace("#", "");
        s = s.Trim();

        // Unwrap a fully-quoted reply, but leave an apostrophe or an internal quote alone.
        if (s.Length > 1)
        {
            char first = s[0];
            char last = s[s.Length - 1];
            bool quoted = (first == '"' && last == '"') || (first == '“' && last == '”');
            if (quoted) s = s.Substring(1, s.Length - 2).Trim();
        }

        // Collapse any run of whitespace so the word count below is accurate.
        var sb = new StringBuilder(s.Length);
        bool lastWasSpace = false;
        foreach (char c in s)
        {
            bool isSpace = char.IsWhiteSpace(c);
            if (isSpace && lastWasSpace) continue;
            sb.Append(isSpace ? ' ' : c);
            lastWasSpace = isSpace;
        }

        return sb.ToString().Trim();
    }

    public static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
