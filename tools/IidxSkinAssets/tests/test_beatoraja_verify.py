import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from beatoraja_verify import package_receipt_path, verify_package


class PackageIntegrityTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-verify-tests"
        parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / "assets").mkdir()
        (self.root / "shards").mkdir()
        self.entry = self.root / "play7.luaskin"
        self.entry.write_text("return {type=0}\n", encoding="utf-8")
        self.asset = b"synthetic test font descriptor"
        sha = hashlib.sha256(self.asset).hexdigest()
        self.asset_path = self.root / "assets" / (sha + ".fnt")
        self.asset_path.write_bytes(self.asset)
        self.receipt = {"contract": "oms-beatoraja-modular-package.v1", "family": "standard",
                        "entryFilesSha256": {"play7.luaskin": hashlib.sha256(self.entry.read_bytes()).hexdigest()},
                        "shards": {}, "assets": {sha: {"bytes": len(self.asset), "sources": []}}}
        self.receipt_path = self.root / "package-receipt.json.txt"
        self.save()

    def save(self):
        self.receipt_path.write_text(json.dumps(self.receipt), encoding="utf-8")

    def test_identity_is_stable_and_covers_entry_and_receipt(self):
        first = verify_package(self.root)
        self.assertEqual(first["payloadSha256"], verify_package(self.root)["payloadSha256"])
        self.assertEqual(package_receipt_path(self.root), self.receipt_path)
        self.assertEqual(first["receiptSha256"], hashlib.sha256(self.receipt_path.read_bytes()).hexdigest())
        self.assertEqual({v["path"] for v in first["inventory"]},
                         {"play7.luaskin", "package-receipt.json.txt", self.asset_path.relative_to(self.root).as_posix()})
        self.entry.write_text("return {type=2}\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "hash/size"):
            verify_package(self.root)

    def test_legacy_receipt_keeps_its_actual_inventory_identity(self):
        canonical = verify_package(self.root)
        legacy = self.root / "package-receipt.json"
        self.receipt_path.rename(legacy)
        result = verify_package(self.root)
        self.assertEqual(package_receipt_path(self.root), legacy)
        self.assertEqual(result["receiptSha256"], canonical["receiptSha256"])
        self.assertNotEqual(result["payloadSha256"], canonical["payloadSha256"])
        self.assertEqual({v["path"] for v in result["inventory"]},
                         {"play7.luaskin", "package-receipt.json", self.asset_path.relative_to(self.root).as_posix()})
        self.assertFalse(self.receipt_path.exists())

    def test_two_receipts_are_rejected_even_with_identical_bytes(self):
        (self.root / "package-receipt.json").write_bytes(self.receipt_path.read_bytes())
        for operation in (package_receipt_path, verify_package):
            with self.subTest(operation=operation.__name__), self.assertRaisesRegex(ValueError, "exactly one package receipt"):
                operation(self.root)

    def test_missing_receipt_is_rejected(self):
        self.receipt_path.unlink()
        with self.assertRaisesRegex(ValueError, "exactly one package receipt"):
            verify_package(self.root)

    def test_changed_asset_is_rejected_even_when_size_matches(self):
        self.asset_path.write_bytes(b"X" * len(self.asset))
        with self.assertRaisesRegex(ValueError, "hash/size"):
            verify_package(self.root)

    def test_unrecorded_lua_is_not_accepted_as_the_same_package(self):
        (self.root / "shards" / "extra.lua").write_text("return {}", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Runtime script inventory"):
            verify_package(self.root)

    def test_missing_asset_does_not_pass_a_receipt_only_check(self):
        self.asset_path.unlink()
        with self.assertRaisesRegex(ValueError, "Asset inventory"):
            verify_package(self.root)


if __name__ == "__main__":
    unittest.main()
