#!/usr/bin/env python3
"""Sonde « préférences » : pose les questions du parsing de préférences sur un texte libre."""

import json
import os
import sys
import time
import urllib.error
import urllib.request

CLEF_URL = os.environ.get("CLEF_URL", "http://localhost:8090/v1/systemone").rstrip("/")
PROBE_TEXT = os.environ.get(
    "PROBE_TEXT",
    "Je veux un quartier vivant, bien desservi, mais pas trop cher.",
)
LOCALE = os.environ.get("PROBE_LOCALE", "fr")

METRIC_SUBJECTS = {
    "population_density": "la densité de population",
    "transport_accessibility": "l'accès aux transports en commun",
    "nightlife": "la vie nocturne",
    "cafes_density": "les cafés",
}

IMPORTANCE_LEVELS = [
    "Pas mentionné ou sans importance",
    "Un peu important",
    "Moyennement important",
    "Très important",
    "Essentiel",
]

DIRECTION_OPTIONS = {
    "high": "En veut plus ou beaucoup",
    "low": "En veut moins ou peu",
    "any": "Aucune préférence exprimée",
}


def build_questions() -> dict:
    questions = {
        "gate__is_neighborhood_request": {
            "type": "noul",
            "instructions": (
                "Ce message décrit-il le type de quartier où cette personne aimerait vivre ?"
                if LOCALE == "fr"
                else "Does this message describe the kind of neighborhood this person would like to live in?"
            ),
        },
        "gate__mentions_reference": {
            "type": "noul",
            "instructions": (
                "Cette personne demande-t-elle un quartier semblable à un lieu précis ?"
                if LOCALE == "fr"
                else "Is this person asking for a neighborhood similar to a specific place?"
            ),
        },
    }
    for metric_key, subject in METRIC_SUBJECTS.items():
        questions[f"importance__{metric_key}"] = {
            "type": "score",
            "instructions": (
                f"Pour cette personne, {subject} : quelle importance dans le choix d'un quartier ?"
                if LOCALE == "fr"
                else f"For this person, how important is {subject} when choosing a neighborhood?"
            ),
            "criteria": IMPORTANCE_LEVELS,
        }
        questions[f"direction__{metric_key}"] = {
            "type": "choice",
            "instructions": (
                f"Concernant {subject}, que souhaite cette personne ?"
                if LOCALE == "fr"
                else f"Regarding {subject}, what does this person want?"
            ),
            "criteria": DIRECTION_OPTIONS,
        }
    return questions


def main() -> int:
    payload = {"model": "clef-flash", "state": PROBE_TEXT[:500], "questions": build_questions()}
    request = urllib.request.Request(
        CLEF_URL,
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )

    started_at = time.monotonic()
    try:
        with urllib.request.urlopen(request, timeout=300) as response:
            elapsed_ms = (time.monotonic() - started_at) * 1000
            body = response.read().decode("utf-8", errors="replace")
            print(f"{response.status} {CLEF_URL} ({len(payload['questions'])} questions, {elapsed_ms:.0f} ms)")
            print(body[:8000])
            return 0
    except urllib.error.HTTPError as error:
        payload_body = error.read().decode("utf-8", errors="replace")
        print(f"HTTP {error.code} depuis {CLEF_URL}", file=sys.stderr)
        print(payload_body[:2000], file=sys.stderr)
        return 1
    except urllib.error.URLError as error:
        print(f"Connexion impossible vers {CLEF_URL} : {error.reason}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
