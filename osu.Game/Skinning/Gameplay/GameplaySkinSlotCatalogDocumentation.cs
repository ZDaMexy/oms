// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Globalization;
using System.Text;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Deterministic documentation and contract views generated exclusively from the public catalog authority.
    /// </summary>
    public static class GameplaySkinSlotCatalogDocumentation
    {
        public static string GenerateCanonicalContract()
        {
            var output = new StringBuilder();
            output.Append("catalog=").Append(GameplaySkinSlotCatalog.CONTRACT_ID).Append('\n');

            foreach (GameplaySkinSlotDescriptor slot in GameplaySkinSlotCatalog.All)
            {
                output.Append(slot.Id).Append('\t')
                      .Append(slot.StableName).Append('\t')
                      .Append(slot.CatalogFamily).Append(':').Append(slot.CatalogVersion.ToString(CultureInfo.InvariantCulture)).Append('\t')
                      .Append(formatFlags(slot.AllowedScopes)).Append('\t')
                      .Append(slot.ValueType).Append('\t')
                      .Append(slot.Classification).Append('\t')
                      .Append(slot.DefaultSemantics).Append('\t')
                      .Append(slot.SuppressEligibility).Append('\t')
                      .Append(formatFlags(slot.Applicability.Rulesets)).Append('\t')
                      .Append(formatFlags(slot.Applicability.Stages)).Append('\t')
                      .Append(formatFlags(slot.Applicability.LaneRoles)).Append('\t')
                      .Append(formatFlags(slot.Applicability.Keymodes)).Append('\t')
                      .Append(slot.Applicability.MinimumKeyCount.ToString(CultureInfo.InvariantCulture)).Append('-')
                      .Append(slot.Applicability.MaximumKeyCount.ToString(CultureInfo.InvariantCulture)).Append('\t')
                      .Append(slot.DiagnosticId).Append('\n');
            }

            return output.ToString();
        }

        public static string GenerateMarkdownTable()
        {
            var output = new StringBuilder();
            output.Append("| ID | Stable name | Catalog | Scope | Type | Class | Default | Suppress | Rulesets | Stage | Lane role | Keymode | Keys | Diagnostic |\n")
                  .Append("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |\n");

            foreach (GameplaySkinSlotDescriptor slot in GameplaySkinSlotCatalog.All)
            {
                output.Append("| `").Append(slot.Id).Append("` | `")
                      .Append(slot.StableName).Append("` | `")
                      .Append(slot.CatalogFamily).Append(':').Append(slot.CatalogVersion.ToString(CultureInfo.InvariantCulture)).Append("` | ")
                      .Append(formatFlags(slot.AllowedScopes)).Append(" | ")
                      .Append(slot.ValueType).Append(" | ")
                      .Append(slot.Classification).Append(" | ")
                      .Append(slot.DefaultSemantics).Append(" | ")
                      .Append(slot.SuppressEligibility).Append(" | ")
                      .Append(formatFlags(slot.Applicability.Rulesets)).Append(" | ")
                      .Append(formatFlags(slot.Applicability.Stages)).Append(" | ")
                      .Append(formatFlags(slot.Applicability.LaneRoles)).Append(" | ")
                      .Append(formatFlags(slot.Applicability.Keymodes)).Append(" | ")
                      .Append(slot.Applicability.MinimumKeyCount.ToString(CultureInfo.InvariantCulture)).Append('-')
                      .Append(slot.Applicability.MaximumKeyCount.ToString(CultureInfo.InvariantCulture)).Append(" | `")
                      .Append(slot.DiagnosticId).Append("` |\n");
            }

            return output.ToString().TrimEnd();
        }

        private static string formatFlags<TEnum>(TEnum value)
            where TEnum : struct, Enum
            => value.ToString().Replace(", ", "/", StringComparison.Ordinal);
    }
}
