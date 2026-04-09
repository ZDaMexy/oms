// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Online
{
    public class ProductionEndpointConfiguration : EndpointConfiguration
    {
        public ProductionEndpointConfiguration()
        {
            // Current OMS local-first builds intentionally ship without default remote endpoints.
            WebsiteUrl = APIUrl = string.Empty;
            APIClientSecret = string.Empty;
            APIClientID = string.Empty;
            SpectatorUrl = string.Empty;
            MultiplayerUrl = string.Empty;
            MetadataUrl = string.Empty;
            BeatmapSubmissionServiceUrl = null;
        }
    }
}
