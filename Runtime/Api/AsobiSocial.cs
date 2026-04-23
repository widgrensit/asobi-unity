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

        public Task<Friendship> UpdateFriendAsync(string friendId, string status)
        {
            var req = new UpdateFriendRequest { status = status };
            return _client.Http.Put<Friendship>($"/api/v1/friends/{friendId}", req);
        }

        public Task<Friendship> AcceptFriendAsync(string friendId)
        {
            return UpdateFriendAsync(friendId, "accepted");
        }

        public Task<Friendship> BlockFriendAsync(string friendId)
        {
            return UpdateFriendAsync(friendId, "blocked");
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

        public Task<Group> UpdateGroupAsync(string groupId, UpdateGroupRequest update)
        {
            return _client.Http.Put<Group>($"/api/v1/groups/{groupId}", update);
        }

        public Task<AsobiResponse> LeaveGroupAsync(string groupId)
        {
            return _client.Http.Post<AsobiResponse>($"/api/v1/groups/{groupId}/leave");
        }

        public Task<GroupMembersResponse> GetGroupMembersAsync(string groupId)
        {
            return _client.Http.Get<GroupMembersResponse>($"/api/v1/groups/{groupId}/members");
        }

        public Task<GroupMember> UpdateMemberRoleAsync(string groupId, string playerId, string role)
        {
            var req = new UpdateMemberRoleRequest { role = role };
            return _client.Http.Put<GroupMember>($"/api/v1/groups/{groupId}/members/{playerId}/role", req);
        }

        public Task<AsobiResponse> RemoveMemberAsync(string groupId, string playerId)
        {
            return _client.Http.Delete($"/api/v1/groups/{groupId}/members/{playerId}");
        }

        // --- Chat ---

        public Task<ChatHistoryResponse> GetChatHistoryAsync(string channelId, int? limit = null)
        {
            Dictionary<string, string> query = null;
            if (limit.HasValue)
                query = new Dictionary<string, string> { { "limit", limit.Value.ToString() } };
            return _client.Http.Get<ChatHistoryResponse>($"/api/v1/chat/{channelId}/history", query);
        }
    }
}
