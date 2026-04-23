using System;

namespace Asobi
{
    [Serializable]
    public class Friendship
    {
        public string id;
        public string player_id;
        public string friend_id;
        public string status;
        public string inserted_at;
        public string updated_at;
    }

    [Serializable]
    public class FriendsResponse
    {
        public Friendship[] friends;
    }

    [Serializable]
    public class AddFriendRequest
    {
        public string friend_id;
    }

    [Serializable]
    public class UpdateFriendRequest
    {
        public string status;
    }

    [Serializable]
    public class Group
    {
        public string id;
        public string name;
        public string description;
        public int max_members;
        public bool open;
        public string creator_id;
        public string inserted_at;
        public string updated_at;
    }

    [Serializable]
    public class CreateGroupRequest
    {
        public string name;
        public string description;
        public int max_members;
        public bool open;
    }

    [Serializable]
    public class UpdateGroupRequest
    {
        public string name;
        public string description;
        public int max_members;
        public bool open;
    }

    [Serializable]
    public class GroupMember
    {
        public string player_id;
        public string role;
        public string joined_at;
    }

    [Serializable]
    public class GroupMembersResponse
    {
        public GroupMember[] members;
    }

    [Serializable]
    public class UpdateMemberRoleRequest
    {
        public string role;
    }

    [Serializable]
    public class ChatMessage
    {
        public string id;
        public string channel_type;
        public string channel_id;
        public string sender_id;
        public string content;
        public string sent_at;
    }

    [Serializable]
    public class ChatHistoryResponse
    {
        public ChatMessage[] messages;
    }
}
