import type { ReactNode } from "react";

interface AccountFieldProps {
  id: string;
  label: ReactNode;
  type?: "email" | "password" | "text";
  autoComplete?: string;
  value: string;
  onChange: (value: string) => void;
  hint?: ReactNode;
  required?: boolean;
}

export function AccountField({
  id,
  label,
  type = "text",
  autoComplete,
  value,
  onChange,
  hint,
  required = true,
}: AccountFieldProps) {
  const hintId = hint ? `${id}-hint` : undefined;

  return (
    <div className="grid gap-1.5">
      <label htmlFor={id} className="text-sm font-semibold">
        {label}
      </label>
      <input
        id={id}
        name={id}
        type={type}
        autoComplete={autoComplete}
        required={required}
        value={value}
        aria-describedby={hintId}
        onChange={(event) => onChange(event.target.value)}
        className="min-h-11 rounded-xl border border-border bg-bg px-3 py-2 text-sm text-text focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
      />
      {hint && (
        <p id={hintId} className="text-xs text-text-secondary">
          {hint}
        </p>
      )}
    </div>
  );
}
