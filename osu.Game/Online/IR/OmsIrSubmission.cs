// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using Newtonsoft.Json.Linq;

namespace osu.Game.Online.IR
{
    /// <summary>
    /// A snapshot of one locally saved play. Retries retain this payload and the actual saved score ID.
    /// </summary>
    public sealed class OmsIrSubmission
    {
        public Guid SubmissionId { get; }

        public JObject Payload { get; }

        public OmsIrSubmission(Guid submissionId, JObject payload)
        {
            if (submissionId == Guid.Empty)
                throw new ArgumentException("A submission requires the saved score ID.", nameof(submissionId));

            SubmissionId = submissionId;
            Payload = (JObject)payload.DeepClone();
        }
    }
}
