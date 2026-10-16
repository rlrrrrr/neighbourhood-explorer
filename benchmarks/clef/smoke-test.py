#!/usr/bin/env python3
"""Smoke test CLEF : envoie une requête /v1/systemone minimale et affiche la réponse."""

import json
import os
import sys
import time
import urllib.error
import urllib.request

CLEF_URL = os.environ.get("CLEF_URL", "http://localhost:8090/v1/systemone").rstrip("/")

REQUEST_PAYLOAD = {
    "model": "clef-flash",
    "state": "Notre page de paiement renvoie des erreurs et les commandes sont bloquées.",
    "questions": {
        "department": {
            "type": "choice",
            "instructions": "Quelle équipe doit traiter ce message ?",
            "criteria": {"billing": "Paiements ou factures", "technical": "Bugs ou pannes"},
        },
        "urgency": {
            "type": "score",
            "instructions": "Quelle est l'urgence de ce message ?",
            "criteria": ["Peut attendre", "Cette semaine", "Aujourd'hui"],
        },
        "outage": {"type": "noul", "instructions": "Un service est-il en panne ?"},
    },
}


def main() -> int:
    body = json.dumps(REQUEST_PAYLOAD).encode("utf-8")
    request = urllib.request.Request(
        CLEF_URL,
        data=body,
        headers={"Content-Type": "application/json"},
        method="POST",
    )

    started_at = time.monotonic()
    try:
        with urllib.request.urlopen(request, timeout=300) as response:
            elapsed_ms = (time.monotonic() - started_at) * 1000
            payload = response.read().decode("utf-8", errors="replace")
            print(f"{response.status} {CLEF_URL} ({elapsed_ms:.0f} ms)")
            print(payload[:4000])
            return 0
    except urllib.error.HTTPError as error:
        payload = error.read().decode("utf-8", errors="replace")
        print(f"HTTP {error.code} depuis {CLEF_URL}", file=sys.stderr)
        print(payload[:2000], file=sys.stderr)
        return 1
    except urllib.error.URLError as error:
        print(f"Connexion impossible vers {CLEF_URL} : {error.reason}", file=sys.stderr)
        print("CLEF tourne-t-il ? (mise clef, puis attendre « healthy »)", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
