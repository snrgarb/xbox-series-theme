using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The call the user is in: the voice channel or DM Discord reports, and the people in it as its voice states and
    /// speaking events have it, in Discord's order.
    /// </summary>
    internal sealed class DiscordCall
    {
        private readonly string name;
        private readonly string selfId;
        private readonly List<DiscordParticipant> people = new List<DiscordParticipant>();
        private readonly HashSet<string> speaking = new HashSet<string>();

        /// <summary>A DM's name from the people last seen in it, kept while nobody else is there, as when ringing.</summary>
        private string peopleName;

        private DiscordCall(string id, string name, string selfId)
        {
            Id = id;
            this.name = name;
            this.selfId = selfId;
        }

        /// <summary>The channel's id.</summary>
        public string Id { get; }

        /// <summary>
        /// The call made from a channel object, as GET_CHANNEL and GET_SELECTED_VOICE_CHANNEL answer, for the user
        /// signed in as <paramref name="selfId"/>; null for none.
        /// </summary>
        public static DiscordCall FromChannel(JObject channel, string selfId)
        {
            var id = (string)channel?["id"];
            if (id == null)
            {
                return null;
            }

            var call = new DiscordCall(id, (string)channel["name"], selfId);
            foreach (var state in (channel["voice_states"] as JArray ?? new JArray()).OfType<JObject>())
            {
                call.Upsert(state);
            }

            return call;
        }

        /// <summary>
        /// The heading: the channel's name; a DM's, which has none, from the other people in it, comma-joined, else
        /// from those last seen there, else "Call".
        /// </summary>
        public string Title => !string.IsNullOrEmpty(name) ? name : peopleName ?? "Call";

        /// <summary>The user first, then the others in Discord's order.</summary>
        public IEnumerable<DiscordParticipant> UserFirst => people.OrderBy(p => p.UserId != selfId);

        /// <summary>Whether the user is speaking.</summary>
        public bool IsUserSpeaking => speaking.Contains(selfId ?? "");

        /// <summary>A voice state come or changed: the person is updated in place, or added after the others.</summary>
        public void Upsert(JObject state)
        {
            var person = DiscordParticipant.FromVoiceState(state);
            if (person == null)
            {
                return;
            }

            var index = people.FindIndex(p => p.UserId == person.UserId);
            if (index < 0)
            {
                people.Add(person);
            }
            else
            {
                people[index] = person;
            }

            RememberPeopleName();
        }

        /// <summary>A voice state gone: the person has left the call.</summary>
        public void Remove(JObject state)
        {
            var userId = (string)state?["user"]?["id"];
            people.RemoveAll(p => p.UserId == userId);
            speaking.Remove(userId);
            RememberPeopleName();
        }

        private void RememberPeopleName()
        {
            var others = string.Join(", ", people.Where(p => p.UserId != selfId).Select(p => p.Name));
            if (others.Length > 0)
            {
                peopleName = others;
            }
        }

        /// <summary>SPEAKING_START or SPEAKING_STOP for <paramref name="userId"/>.</summary>
        public void SetSpeaking(string userId, bool isSpeaking)
        {
            if (userId == null)
            {
                return;
            }

            if (isSpeaking)
            {
                speaking.Add(userId);
            }
            else
            {
                speaking.Remove(userId);
            }
        }

        public bool IsSpeaking(string userId) => userId != null && speaking.Contains(userId);
    }

    /// <summary>One person in a call, as their voice state has them.</summary>
    internal sealed class DiscordParticipant
    {
        private DiscordParticipant(string userId, string name, string avatar, bool isMuted, bool isDeafened)
        {
            UserId = userId;
            Name = name;
            Avatar = avatar;
            IsMuted = isMuted;
            IsDeafened = isDeafened;
        }

        public string UserId { get; }

        /// <summary>Their nickname on the server, else their display name, else their username.</summary>
        public string Name { get; }

        /// <summary>Their avatar's URL, or Discord's default one for them when they have none.</summary>
        public string Avatar { get; }

        /// <summary>Muted, by themselves or the server.</summary>
        public bool IsMuted { get; }

        /// <summary>Deafened, by themselves or the server.</summary>
        public bool IsDeafened { get; }

        /// <summary>The person a voice state stands for; null without a user.</summary>
        public static DiscordParticipant FromVoiceState(JObject state)
        {
            var user = state?["user"] as JObject;
            var id = (string)user?["id"];
            if (id == null)
            {
                return null;
            }

            var name = new[] { (string)state["nick"], (string)user["global_name"], (string)user["username"] }.FirstOrDefault(n => !string.IsNullOrEmpty(n));
            var voice = state["voice_state"] as JObject;
            return new DiscordParticipant(
                id,
                name ?? "",
                AvatarUrl(id, (string)user["avatar"]),
                IsSet(voice, "mute") || IsSet(voice, "self_mute"),
                IsSet(voice, "deaf") || IsSet(voice, "self_deaf"));
        }

        private static bool IsSet(JObject voice, string flag) => (bool?)voice?[flag] == true;

        private static string AvatarUrl(string userId, string avatar)
        {
            if (!string.IsNullOrEmpty(avatar))
            {
                return "https://cdn.discordapp.com/avatars/" + userId + "/" + avatar + ".png?size=64";
            }

            ulong.TryParse(userId, NumberStyles.None, CultureInfo.InvariantCulture, out var snowflake);
            return "https://cdn.discordapp.com/embed/avatars/" + ((snowflake >> 22) % 6).ToString(CultureInfo.InvariantCulture) + ".png";
        }
    }
}
