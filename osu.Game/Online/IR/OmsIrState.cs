// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Net;
using Newtonsoft.Json;

namespace osu.Game.Online.IR
{
    public sealed record OmsIrAccount(long Id, string Username);

    /// <summary>The service and account selected when this play began.</summary>
    public sealed record OmsIrSubmissionTarget(Uri ServiceUri, long UserId);

    public sealed record OmsIrState(string ServiceAddress, bool Enabled, OmsIrAccount? Account, int PendingCount, int BlockedCount,
                                   int WaitingOtherAccountCount, bool RequiresLogin, bool Busy, string Message);

    public sealed record OmsIrPendingSubmission(Guid SubmissionId, OmsIrSubmissionTarget Target, string? BlockedReason);

    public sealed class OmsIrException : Exception
    {
        public string Code { get; }
        public HttpStatusCode? StatusCode { get; }
        public TimeSpan? RetryAfter { get; }

        public OmsIrException(string code, string message, HttpStatusCode? statusCode = null, TimeSpan? retryAfter = null, Exception? innerException = null)
            : base(message, innerException)
        {
            Code = code;
            StatusCode = statusCode;
            RetryAfter = retryAfter;
        }
    }

    /// <summary>Only the credential store may persist these fields.</summary>
    public sealed class OmsIrSession
    {
        public long UserId { get; }
        public string Username { get; }
        public string AccessToken { get; }
        public string RefreshToken { get; }
        public DateTimeOffset AccessExpiresAt { get; }

        [JsonConstructor]
        public OmsIrSession(long userId, string username, string accessToken, string refreshToken, DateTimeOffset accessExpiresAt)
        {
            if (userId <= 0 || string.IsNullOrEmpty(username) || username.Length is < 3 or > 24
                || !username.All(character => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
                || !ValidToken(accessToken) || !ValidToken(refreshToken)
                || accessExpiresAt == default)
                throw new ArgumentException("Invalid OMS IR credential record.");

            UserId = userId;
            Username = username;
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            AccessExpiresAt = accessExpiresAt;
        }

        // Record-generated ToString() would disclose the tokens when inspecting a session.
        public override string ToString() => "OMS IR credential record";

        internal static bool ValidToken(string value) => !string.IsNullOrEmpty(value) && value.Length is >= 16 and <= 256
            && value.All(character => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '.' or '_' or '~' or '+' or '/' or '=');
    }
}
