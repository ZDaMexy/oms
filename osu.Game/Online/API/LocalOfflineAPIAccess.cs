// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Online.Notifications.WebSocket;

namespace osu.Game.Online.API
{
    internal partial class LocalOfflineAPIAccess : Component, IAPIProvider
    {
        private const string disabled_message = "Online features are disabled in this build.";

        private readonly OsuGameBase game;
        private readonly Bindable<APIState> state = new Bindable<APIState>(APIState.Offline);
        private readonly OfflineLocalUserState localUserState = new OfflineLocalUserState();

        public IBindable<APIUser> LocalUser => localUserState.User;

        public ILocalUserState LocalUserState => localUserState;

        public Language Language => game.CurrentLanguage.Value;

        public string AccessToken => string.Empty;

        public Guid SessionIdentifier { get; } = Guid.NewGuid();

        public bool IsLoggedIn => false;

        public string ProvidedUsername { get; private set; }

        public EndpointConfiguration Endpoints { get; }

        public int APIVersion { get; }

        public Exception? LastLoginError { get; private set; }

        public IBindable<APIState> State => state;

        public SessionVerificationMethod? SessionVerificationMethod => null;

        public INotificationsClient NotificationsClient { get; } = new LocalOfflineNotificationsClient();

        public LocalOfflineAPIAccess(OsuGameBase game, OsuConfigManager config, EndpointConfiguration endpoints)
        {
            this.game = game;

            if (game.IsDeployedBuild)
                APIVersion = game.AssemblyVersion.Major * 10000 + game.AssemblyVersion.Minor;
            else
            {
                var now = DateTimeOffset.Now;
                APIVersion = now.Year * 10000 + now.Month * 100 + now.Day;
            }

            Endpoints = endpoints;
            ProvidedUsername = config.Get<string>(OsuSetting.Username);
        }

        public void Queue(APIRequest request)
        {
            request.AttachAPI(this);
            request.Fail(new WebException(disabled_message));
        }

        public void Perform(APIRequest request)
        {
            request.AttachAPI(this);
            request.Fail(new WebException(disabled_message));
        }

        public Task PerformAsync(APIRequest request)
        {
            request.AttachAPI(this);
            request.Fail(new WebException(disabled_message));
            return Task.CompletedTask;
        }

        public void Login(string username, string password)
        {
            ProvidedUsername = username;
            LastLoginError = new APIException(disabled_message, new InvalidOperationException(disabled_message));

            state.Value = APIState.Connecting;
            Schedule(() => state.Value = APIState.Offline);
        }

        public void AuthenticateSecondFactor(string code)
        {
            LastLoginError = new APIException(disabled_message, new InvalidOperationException(disabled_message));

            state.Value = APIState.Connecting;
            Schedule(() => state.Value = APIState.Offline);
        }

        public void Logout()
        {
            LastLoginError = null;
            state.Value = APIState.Offline;
        }

        void IAPIProvider.Schedule(Action action) => Schedule(action);

        public IHubClientConnector? GetHubConnector(string clientName, string endpoint) => null;

        public IChatClient GetChatClient() => new LocalOfflineChatClient();

        public RegistrationRequest.RegistrationRequestErrors CreateAccount(string email, string username, string password) => new RegistrationRequest.RegistrationRequestErrors
        {
            Message = disabled_message
        };

        private sealed class OfflineLocalUserState : ILocalUserState
        {
            public Bindable<APIUser> User { get; } = new Bindable<APIUser>(new GuestUser());

            public BindableList<APIRelation> Friends { get; } = new BindableList<APIRelation>();

            public BindableList<APIRelation> Blocks { get; } = new BindableList<APIRelation>();

            public BindableList<int> FavouriteBeatmapSets { get; } = new BindableList<int>();

            IBindable<APIUser> ILocalUserState.User => User;

            IBindableList<APIRelation> ILocalUserState.Friends => Friends;

            IBindableList<APIRelation> ILocalUserState.Blocks => Blocks;

            IBindableList<int> ILocalUserState.FavouriteBeatmapSets => FavouriteBeatmapSets;

            public void UpdateFriends()
            {
            }

            public void UpdateBlocks()
            {
            }

            public void UpdateFavouriteBeatmapSets()
            {
            }
        }

        private sealed class LocalOfflineChatClient : IChatClient
        {
            public event Action<Channel>? ChannelJoined
            {
                add
                {
                }

                remove
                {
                }
            }

            public event Action<Channel>? ChannelParted
            {
                add
                {
                }

                remove
                {
                }
            }

            public event Action<List<Message>>? NewMessages
            {
                add
                {
                }

                remove
                {
                }
            }

            public event Action? PresenceReceived
            {
                add
                {
                }

                remove
                {
                }
            }

            public void RequestPresence()
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class LocalOfflineNotificationsClient : INotificationsClient
        {
            private readonly BindableBool isConnected = new BindableBool(false);

            public IBindable<bool> IsConnected => isConnected;

            public event Action<SocketMessage>? MessageReceived
            {
                add
                {
                }

                remove
                {
                }
            }

            public Task SendAsync(SocketMessage message, CancellationToken? cancellationToken = default) => Task.CompletedTask;
        }
    }
}
