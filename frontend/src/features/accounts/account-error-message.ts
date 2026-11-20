import { fbt, type TranslatedString } from "fbtee";

export function accountErrorMessage({
  code,
  errors,
}: {
  code: string;
  errors: readonly string[];
}): TranslatedString {
  switch (code) {
    case "invalid_credentials":
      return fbt(
        "Courriel ou mot de passe incorrect.",
        "Login error when the email or password is wrong",
      );
    case "email_not_confirmed":
      return fbt(
        "Confirme d'abord ton adresse électronique grâce au lien reçu par courriel.",
        "Login error when the account email has not been confirmed",
      );
    case "account_locked":
      return fbt(
        "Compte temporairement verrouillé après plusieurs essais. Réessaie dans quinze minutes.",
        "Login error when the account is locked out",
      );
    case "rate_limited":
      return fbt(
        "Trop de tentatives en peu de temps. Patiente une minute avant de réessayer.",
        "Error when the authentication rate limit is reached",
      );
    case "invalid_token":
      return fbt(
        "Ce lien de confirmation n'est plus valide. Demande un nouveau courriel.",
        "Error when the email confirmation token is invalid or expired",
      );
    case "csrf_failed":
      return fbt(
        "La session a expiré. Recharge la page puis réessaie.",
        "Error when the anti-forgery token is rejected",
      );
    case "not_authenticated":
      return fbt(
        "Connecte-toi pour continuer.",
        "Error when an authenticated action is attempted while signed out",
      );
    case "favorite_target_unknown":
      return fbt(
        "Ce quartier n'est pas encore disponible dans le catalogue.",
        "Error when a favorite target is not in the catalog",
      );
    case "validation_failed":
      return validationErrorMessage(errors);
    default:
      return fbt(
        "Une erreur est survenue. Réessaie dans un instant.",
        "Generic account error message",
      );
  }
}

export function validationErrorMessage(errors: readonly string[]): TranslatedString {
  switch (errors[0]) {
    case "email_invalid":
      return fbt("Cette adresse électronique n'est pas valide.", "Validation error for the email");
    case "display_name_invalid":
      return fbt(
        "Le nom affiché doit contenir entre 1 et 80 caractères.",
        "Validation error for the display name",
      );
    case "password_required":
      return fbt("Choisis un mot de passe.", "Validation error when the password is empty");
    case "password_too_short":
      return fbt(
        "Le mot de passe doit contenir au moins 12 caractères.",
        "Validation error when the password is too short",
      );
    case "password_requires_digit":
      return fbt(
        "Le mot de passe doit contenir au moins un chiffre.",
        "Validation error when the password lacks a digit",
      );
    case "password_requires_lower":
      return fbt(
        "Le mot de passe doit contenir au moins une minuscule.",
        "Validation error when the password lacks a lowercase letter",
      );
    case "password_requires_upper":
      return fbt(
        "Le mot de passe doit contenir au moins une majuscule.",
        "Validation error when the password lacks an uppercase letter",
      );
    case "password_requires_non_alphanumeric":
      return fbt(
        "Le mot de passe doit contenir au moins un caractère spécial.",
        "Validation error when the password lacks a special character",
      );
    case "email_taken":
      return fbt(
        "Cette adresse est déjà utilisée. Connecte-toi ou choisis-en une autre.",
        "Validation error when the email is already registered",
      );
    default:
      return fbt(
        "Les informations saisies ne sont pas valides.",
        "Generic validation error message",
      );
  }
}
