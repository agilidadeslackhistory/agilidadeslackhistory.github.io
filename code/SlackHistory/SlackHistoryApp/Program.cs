using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace SlackHistory
{
    class Program
    {
    	//replace token xoxp-26200241765-57896078608-64729098982-940f37bca0 by your own
    	//look at saveFile to adjust the path
    	
        static Dictionary<string, string> channels = new Dictionary<string, string>();
        static Dictionary<string, Member> users = new Dictionary<string, Member>();
        static string htmlTemplate =
    @"
    <!DOCTYPE html>
    <html lang=""pt-br"">
    <head>
        <meta charset=""utf-8""/>
        <title>PARAM0</title>
        <style>
            * {
				font-family:courier;
			}
			body {
                text-align:center;
				padding:1em;
			}
			.messages {
				width:100%;
				text-align:left;
			}
			.messages img {
				background-color:rgb(248,244,240);
				width:36px;
				height:36px;
				border-radius:0.2em;
				vertical-align:top;
				margin-right:0.65em;
			}
			.messages .time {
				display:inline-block;
				color:rgb(200,200,200);
				margin-left:0.5em;
			}
			.messages .username {
				font-weight:600;
				line-height:1;
			}
			.messages .message {
				vertical-align:top;
				line-height:1;
				width:calc(100% - 3em);
			}
			.messages .message .msg {
				line-height:1.5;
			}
            .cite {
				color:blue;
                font-weight:bold;
			}
        </style>
    </head>
    <body>
        PARAM1
    </body>
    </html>
";

        static void Main(string[] args)
        {
            getUsers();
            getChannels();
            //get only yesterday posts
            getPosts(1);
        }

        static void getUsers()
        {
            using (WebClient client = new WebClient())
            {
                string response = client.DownloadString("https://slack.com/api/users.list?token=xoxp-26200241765-57896078608-64729098982-940f37bca0");
                UserWrapper wrapper = new JavaScriptSerializer().Deserialize<UserWrapper>(response);
                foreach (var member in wrapper.members)
                {
                    users.Add(member.id, member);
                }
            }
        }

        static void getChannels()
        {
            using (WebClient client = new WebClient())
            {
                string response = client.DownloadString("https://slack.com/api/channels.list?token=xoxp-26200241765-57896078608-64729098982-940f37bca0");
                ChannelWrapper wrapper = new JavaScriptSerializer().Deserialize<ChannelWrapper>(response);

                foreach (var channel in wrapper.channels)
                {
                    channels.Add(channel.id, channel.name);
                }
            }
        }

        static void getPosts(int i)
        {
            var baseUrl = "https://slack.com/api/channels.history?token=xoxp-26200241765-57896078608-64729098982-940f37bca0&channel={0}&count=1000&inclusive=1&oldest={1}&latest={2}";
            DateTime baseDate = DateTime.Now.AddDays(-1 * i).ToUniversalTime();
            DateTime oldest = new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, 0, 0, 0, DateTimeKind.Utc);
            DateTime latest = new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, 23, 59, 59, DateTimeKind.Utc);
            foreach (var channel in channels)
            {
                using (WebClient client = new WebClient())
                {
                    string response = client.DownloadString(string.Format(baseUrl, channel.Key, oldest.ToUnixTime(), latest.ToUnixTime()));
                    MessageWrapper wrapper = new JavaScriptSerializer().Deserialize<MessageWrapper>(response);
                    var title = channel.Value + "." + DateTime.Now.AddDays(-1 * i).ToString("yyyyMMdd");
                    var messages = handleMessages(wrapper, title);
                    saveFile(messages, channel.Value, title);
                }
            }
        }

        static void saveFile(string contents, string channel, string title){
            var basePath = @"c:\temp\slack\";
            var currentFolder = Path.Combine(basePath, channel);
            if (!Directory.Exists(currentFolder)) Directory.CreateDirectory(currentFolder);
            var fileName = Path.Combine(currentFolder, title + ".html");
            if (File.Exists(fileName)) File.Delete(fileName);

            File.AppendAllText(fileName, contents);
        }

        static string handleMessages(MessageWrapper wrapper, string title)
        {
            var messagesHandled = new List<string>();

            for (int i=wrapper.messages.Count - 1; i>=0; i--)
            {
                var message = wrapper.messages[i];

                if (message.text.Contains("has joined the channel")) continue;
                if (message.text.Contains("has left the channel")) continue;

                var messageWithReplacements = message.text;

                try
                {
                    messageWithReplacements = Regex.Replace(messageWithReplacements, "<@(.*?)\\|(.*?)>", m => "@" + m.Groups[2].Value);
                    messageWithReplacements = Regex.Replace(messageWithReplacements, "<@(.*?)>", m => "<span class=\"cite\">@" + users[m.Groups[1].Value].name + "</span>");
                    messageWithReplacements = Regex.Replace(messageWithReplacements, "<#C(.*?)\\|(.*?)>", m => "#" + m.Groups[2].Value);
                    messageWithReplacements = Regex.Replace(messageWithReplacements, "<#C(.*?)>", m => "#" + channels["C" + m.Groups[1].Value]);
                }
                catch
                {
                    messageWithReplacements = message.text;
                }
                

                messagesHandled.Add(
                        "<p class=\"messages\"><span class=\"username\">" + 
                        (message.user!=null?users[message.user].name:"??") + 
                        "</span><span class=\"time\"> " + 
                        long.Parse(message.ts.Split('.')[0]).FromUnixTime().ToString("hh:mm") + 
                        "</span> >> " + 
                        messageWithReplacements + 
                        "</p>");
            }
            if (messagesHandled.Count == 0)
                return htmlTemplate.Replace("PARAM0", title).Replace("PARAM1", "NO MESSAGES :(");   
            else
                return htmlTemplate.Replace("PARAM0", title).Replace("PARAM1", string.Join(Environment.NewLine, messagesHandled));   
        }
    }

    #region helpers
    public class Reaction
    {
        public string name { get; set; }
        public List<string> users { get; set; }
        public int count { get; set; }
    }

    public class Edited
    {
        public string user { get; set; }
        public string ts { get; set; }
    }

    public class Message
    {
        public string type { get; set; }
        public string user { get; set; }
        public string text { get; set; }
        public string ts { get; set; }
        public List<Reaction> reactions { get; set; }
        public Edited edited { get; set; }
    }

    public class MessageWrapper
    {
        public bool ok { get; set; }
        public string latest { get; set; }
        public string oldest { get; set; }
        public List<Message> messages { get; set; }
        public bool has_more { get; set; }
        public bool is_limited { get; set; }
    }
    public class ChannelWrapper
    {
        public string ok { get; set; }
        public Channel[] channels { get; set; }
    }

    public class Channel
    {
        public string id { get; set; }
        public string name { get; set; }
        public string is_channel { get; set; }
        public string created { get; set; }
        public string creator { get; set; }
        public string is_archived { get; set; }
        public string is_general { get; set; }
        public string is_member { get; set; }
        public string[] members { get; set; }
        public Topic topic { get; set; }
        public Purpose purpose { get; set; }
        public string num_members { get; set; }
    }

    public class Topic
    {
        public string value { get; set; }
        public string creator { get; set; }
        public string last_set { get; set; }
    }

    public class Purpose
    {
        public string value { get; set; }
        public string creator { get; set; }
        public string last_set { get; set; }
    }

    public class UserWrapper
    {
        public string ok { get; set; }
        public Member[] members { get; set; }
    }

    public class Profile
    {
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string avatar_hash { get; set; }
        public string real_name { get; set; }
        public string real_name_normalized { get; set; }
        public string email { get; set; }
        public string image_24 { get; set; }
        public string image_32 { get; set; }
        public string image_48 { get; set; }
        public string image_72 { get; set; }
        public string image_192 { get; set; }
        public string image_512 { get; set; }
    }

    public class Member
    {
        public string id { get; set; }
        public string team_id { get; set; }
        public string name { get; set; }
        public bool deleted { get; set; }
        public object status { get; set; }
        public string color { get; set; }
        public string real_name { get; set; }
        public string tz { get; set; }
        public string tz_label { get; set; }
        public int tz_offset { get; set; }
        public Profile profile { get; set; }
        public bool is_admin { get; set; }
        public bool is_owner { get; set; }
        public bool is_primary_owner { get; set; }
        public bool is_restricted { get; set; }
        public bool is_ultra_restricted { get; set; }
        public bool is_bot { get; set; }
    }
    #endregion

    #region extensions
    public static class DtExtensions
    {
        public static DateTime FromUnixTime(this long unixTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(unixTime);
        }

        public static long ToUnixTime(this DateTime date)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return Convert.ToInt64((date.ToUniversalTime() - epoch).TotalSeconds);
        }
    }
#endregion

}
