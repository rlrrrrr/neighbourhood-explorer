import { fbs } from "fbtee";
import { useRef, useState, type ChangeEvent, type FormEvent, type KeyboardEvent } from "react";
import { Button } from "../../components/ui/button";

interface ChatComposerProps {
  onSubmit?: (text: string) => void;
}

export function ChatComposer({ onSubmit }: ChatComposerProps) {
  const [draft, setDraft] = useState("");
  const textAreaRef = useRef<HTMLTextAreaElement>(null);

  function resizeToContent(textArea: HTMLTextAreaElement) {
    textArea.style.height = "auto";
    textArea.style.height = `${textArea.scrollHeight}px`;
  }

  function handleChange(event: ChangeEvent<HTMLTextAreaElement>) {
    setDraft(event.target.value);
    resizeToContent(event.currentTarget);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const text = draft.trim();
    if (text.length === 0 || !onSubmit) {
      return;
    }
    setDraft("");
    if (textAreaRef.current) {
      textAreaRef.current.style.height = "auto";
    }
    onSubmit(text);
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === "Enter" && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault();
      event.currentTarget.form?.requestSubmit();
    }
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="flex items-end gap-2 rounded-3xl border border-border bg-surface p-2 focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-action"
    >
      <textarea
        ref={textAreaRef}
        id="chat-command"
        value={draft}
        onChange={handleChange}
        onKeyDown={handleKeyDown}
        rows={1}
        placeholder={fbs(
          "Ex. un quartier calme avec des cafés, près du métro",
          "Placeholder of the chat input, with an example search",
        )}
        className="max-h-40 min-h-11 flex-1 resize-none overflow-y-auto bg-transparent px-3 py-2.5 text-base outline-none placeholder:text-text-secondary"
      />
      <Button
        type="submit"
        size="icon"
        disabled={draft.trim().length === 0}
        aria-label={fbs("Envoyer", "Label of the chat send button")}
      >
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          className="size-4"
          aria-hidden="true"
        >
          <path d="M12 19V5" />
          <path d="m5 12 7-7 7 7" />
        </svg>
      </Button>
    </form>
  );
}
