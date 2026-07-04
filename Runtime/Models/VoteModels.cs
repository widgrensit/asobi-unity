using System;

namespace Asobi
{
    [Serializable]
    public class Vote
    {
        public string id;
        public string match_id;
        public string template;
        public string method;
        public string options;
        public string votes_cast;
        public string result;
        public string distribution;
        public float turnout;
        public int eligible_count;
        public int window_ms;
        public string opened_at;
        public string closed_at;
        public string inserted_at;
    }

    [Serializable]
    public class VoteListResponse
    {
        public Vote[] votes;
    }
}
