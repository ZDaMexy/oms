"""Failure-source boundaries with synthetic metadata only."""
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, call, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_failure import export_failure, validate_failure_source


class StandardFailureTests(unittest.TestCase):
    @staticmethod
    def source():
        return {"movie": "miss", "stage": [0, 0, 1280, 720], "fps": 60,
                "sourceFrames": 180, "labels": {}, "translatedRootControl": []}

    def test_complete_source_movie_is_accepted(self):
        validate_failure_source(self.source())

    def test_partial_caller_duration_does_not_replace_full_asset(self):
        with self.assertRaises(ValueError):
            validate_failure_source({**self.source(), "sourceFrames": 152})

    def test_another_source_canvas_or_unqualified_loop_is_rejected(self):
        for changed in ({"stage": [0, 0, 1920, 1080]}, {"fps": 30}, {"movie": "failed_0"},
                        {"labels": {"loop": 0}}, {"translatedRootControl": [{"heldLoop": [0, 20]}]}):
            with self.subTest(change=changed), self.assertRaises(ValueError):
                validate_failure_source({**self.source(), **changed})

    def test_export_uses_the_actual_public_failure_event(self):
        exporter = SimpleNamespace(source_library_sha="synthetic", source_families=("0", "1"), files={}, clip=Mock(return_value={
            "source": self.source(), "resources": [], "animations": [], "node": {}}))
        with tempfile.TemporaryDirectory() as directory, patch("standard_failure.StandardComponents", return_value=exporter):
            output = Path(directory)
            result = export_failure(output, output, output, output)
            self.assertEqual([
                call("0/gameparts", "miss", "sf.failed", loop=False, trigger="gameplay.fail"),
                call("1/gameparts", "miss", "sf.failed.legacy", loop=False, trigger="gameplay.fail"),
            ], exporter.clip.call_args_list)
            self.assertEqual("oms-standard-failure.v2", result["contract"])
            self.assertEqual({"0", "1"}, set(result["families"]))
            for family in result["families"].values():
                self.assertEqual(180, family["clip"]["source"]["sourceFrames"])
            self.assertTrue((output / "failure.json").is_file())


if __name__ == "__main__":
    unittest.main()
