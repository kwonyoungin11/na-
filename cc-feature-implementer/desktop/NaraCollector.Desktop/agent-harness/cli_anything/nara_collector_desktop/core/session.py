from __future__ import annotations

import json
from dataclasses import asdict
from pathlib import Path
from typing import Any

from cli_anything.nara_collector_desktop.utils.nara_collector_desktop_backend import (
    SessionSnapshot,
    default_snapshot,
    session_file,
)


class SessionManager:
    def __init__(self, file_path: Path | None = None) -> None:
        self.file_path = file_path or session_file()
        self.file_path.parent.mkdir(parents=True, exist_ok=True)
        if not self.file_path.exists():
            self._write_payload({
                "current": default_snapshot().to_dict(),
                "undo_stack": [],
                "redo_stack": [],
            })

    def load(self) -> dict[str, Any]:
        payload = json.loads(self.file_path.read_text(encoding="utf-8"))
        payload.setdefault("undo_stack", [])
        payload.setdefault("redo_stack", [])
        payload.setdefault("current", default_snapshot().to_dict())
        return payload

    def current(self) -> SessionSnapshot:
        return SessionSnapshot(**self.load()["current"]).normalized()

    def save_current(self, snapshot: SessionSnapshot, *, push_undo: bool = True) -> SessionSnapshot:
        payload = self.load()
        normalized = snapshot.normalized().to_dict()
        if push_undo:
            payload["undo_stack"].append(payload["current"])
            payload["redo_stack"] = []
        payload["current"] = normalized
        self._write_payload(payload)
        return SessionSnapshot(**normalized)

    def update(self, **changes: Any) -> SessionSnapshot:
        current = self.current()
        data = asdict(current)
        data.update({key: value for key, value in changes.items() if value is not None})
        return self.save_current(SessionSnapshot(**data))

    def undo(self) -> SessionSnapshot:
        payload = self.load()
        if not payload["undo_stack"]:
            return self.current()
        payload["redo_stack"].append(payload["current"])
        payload["current"] = payload["undo_stack"].pop()
        self._write_payload(payload)
        return SessionSnapshot(**payload["current"]).normalized()

    def redo(self) -> SessionSnapshot:
        payload = self.load()
        if not payload["redo_stack"]:
            return self.current()
        payload["undo_stack"].append(payload["current"])
        payload["current"] = payload["redo_stack"].pop()
        self._write_payload(payload)
        return SessionSnapshot(**payload["current"]).normalized()

    def reset(self) -> SessionSnapshot:
        snapshot = default_snapshot()
        self._write_payload({
            "current": snapshot.to_dict(),
            "undo_stack": [],
            "redo_stack": [],
        })
        return snapshot

    def _write_payload(self, payload: dict[str, Any]) -> None:
        self.file_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
