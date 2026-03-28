using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asobi
{
    public class AsobiSocial
    {
        readonly AsobiClient _client;
        internal AsobiSocial(AsobiClient client) => _client = client;

        // --- Friends ---

        public Task<FriendsResponse> GetFriendsAsync(string status = null, int limit = 50)
        {
            var query = new Dictionary<string, string> { { "limit", limit.ToString() } };
            if (status != null)
                query["status"] = status;
            return _client.Http.Get<FriendsResponse>("/api/v1/friends", query);
        }

        public Task<Friendship> AddFriendAsync(string friendId)
        {
            var req = new AddFriendRequest { friend_id = friendId };
            return _client.Http.Post<Friendship>("/api/v1/friends", req);
        }

        public Task<Friendship> AcceptFriendAsync(string friendId)
        {
            var req = new UpdateFriendRequest { status = "accepted" };
            return _client.Http.Put<Friendship>($"/api/v1/friends/{friendId}", req);
        }

        public Task<Friendship> BlockFriendAsync(string friendId)
        {
            var req = new UpdateFriendRequest { status = "blocked" };
            return _client.Http.Put<Friendship>($"/api/v1/friends/{friendId}", req);
        }

        public Task<AsobiResponse> RemoveFriendAsync(string friendId)
        {
            return _client.Http.Delete($"/api/v1/friends/{friendId}");
        }

        // --- Groups ---

        public Task<Group> CreateGroupAsync(string name, string description = null, int maxMembers = 50, bool open = false)
        {
            var req = new CreateGroupRequest
            {
                name = name,
                description = description,
                max_members = maxMembers,
                open = open
            };
            return _client.Http.Post<Group>("/api/v1/groups", req);
        }

        public Task<Group> GetGroupAsync(string groupId)
        {
            return _client.Http.Get<Group>($"/api/v1/groups/{groupId}");
        }

        public Task<AsobiResponse> JoinGroupAsync(string groupId)
        {
            return _client.Http.Post<AsobiResponse>($"/api/v1/groups/{groupId}/join");
        }

        public Task<AsobiResponse> LeaveGroupAsync(string groupId)
        {
            return _client.Http.Post<AsobiResponse>($"/api/v1/groups/{groupId}/leave");
        }

        // --- Chat ---

        public Task<ChatHistoryResponse> GetChatHistoryAsync(string channelId)
        {
            return _client.Http.Get<ChatHistoryResponse>($"/api/v1/chat/{channelId}/history");
        }
    }
}
