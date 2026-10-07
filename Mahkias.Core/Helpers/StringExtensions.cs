using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;


namespace Mahkias.Core.Helpers
{
    public static class StringExtensions
    {
        public static string AddHttpsPrfix(this string link)
        {
            if(string.IsNullOrEmpty(link) || link.StartsWith("https://") || link.StartsWith("http://")) return link;

            return "https://" + link;

        }


        public static string TotalMinutesToDurationShortForm(this int durationMinutes)
        {

            int days = durationMinutes / 1440;
            int hours = (durationMinutes % 1440) / 60;
            int mins = durationMinutes % 60;

            var duration = "";
            if (days > 0)
            {
                duration += days.ToString() + "d ";
            }
            if (hours > 0)
            {
                duration += hours.ToString() + "hr ";
              
            }
            if (mins > 0)
            {
                duration += mins.ToString() + "min";
            }



            return duration;
        }

        public static string RemoveAirlineCodeFromFlightNumber(this string flightNumber)
        {
            if (!string.IsNullOrEmpty(flightNumber) && flightNumber.Count() > 2)
            {
                return flightNumber.Substring(2, flightNumber.Count() - 2);
            }
            return flightNumber;
        }


        public static string RemoveAirlineCodeFromFlightNumber(this string flightNumber, string airLineCode)
        {

            if (!string.IsNullOrEmpty(flightNumber))
            {
                if (!string.IsNullOrEmpty(airLineCode) && flightNumber.StartsWith(airLineCode))
                {
                    return flightNumber.Substring(airLineCode.Length, flightNumber.Count() - airLineCode.Length);
                }
                else if (flightNumber.Count() > 2)
                {
                    return flightNumber.Substring(2, flightNumber.Count() - 2);
                }
            }
            return flightNumber;
        }


        public static string GetCompleteEmailsWithinLimit(this string emailList, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(emailList))
                return string.Empty;

            var splitEmails = emailList.Split(new char[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries);
            emailList = string.Join(",", splitEmails.Select(e => e.Contains("<") ? e.Substring(e.IndexOf("<") + 1, e.IndexOf(">") - e.IndexOf("<") - 1) : e));

            if (emailList.Length <= maxLength)
            {
                return emailList;
            }

            string[] emails = emailList.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
            List<string> selectedEmails = new List<string>();
            int currentLength = 0;

            foreach (string email in emails)
            {
                int emailLength = email.Length + 2; // Account for ", " after each email
                if (currentLength + emailLength > maxLength)
                    break;

                selectedEmails.Add(email);
                currentLength += emailLength;
            }

            return string.Join(", ", selectedEmails);
        }

        public static string CreateMD5(byte[] arrayOfBytes)
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(arrayOfBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }

                return sb.ToString();
            }
        }

        public static bool SlugHasSpecialCharacters(this string slug)
        {
            return Regex.IsMatch(slug, @"[^a-zA-Z0-9-]+");
        }


        public static string GetHMAC(string text, string key)
        {
            key ??= "";

            using var hmacsha256 = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmacsha256.ComputeHash(Encoding.UTF8.GetBytes(text));
            return Convert.ToBase64String(hash);
        }

        public static string RemoveAssistantAIAnnotations(this string text)
        {
            string pattern = @"【.*?】"; // Regex pattern to match text between 【 and 】 including the brackets
            return Regex.Replace(text, pattern, string.Empty);
        }

        public static string RemoveDiacritics(this string Text)
        {
            return new Regex(@"\p{Mn}", RegexOptions.Compiled).Replace(Text.Normalize(NormalizationForm.FormD), string.Empty);
        }

        public static string RemoveNonAscii(this string Text)
        {
            return Regex.Replace(Text, @"[^\u0020-\u007E]", string.Empty);
        }

        public static string RemoveSpecialCharacters(this string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                if (c >= '0' && c <= '9' || c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c == '.' || c == '_')
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static string WrapParagraphTags(this string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var values = value.Trim().Split(new char[2] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);


            StringBuilder sb = new StringBuilder();

            foreach (var item in values)
            {

                sb.Append("<p>");
                sb.Append(item);
                sb.Append("</p>");
            }

            return sb.ToString();
        }

        public static string WrapParagraphTagsIfDoesNotExist(this string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var values = value.Trim().Split(new char[2] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            //check if already wrapped
            if (value.StartsWith("<p>") && value.EndsWith("</p>"))
            {
                return value;
            }

            StringBuilder sb = new StringBuilder();

            foreach (var item in values)
            {

                sb.Append("<p>");
                sb.Append(item);
                sb.Append("</p>");
            }

            return sb.ToString();
        }

        public static string ConvertLineBreaksToHtmlBrTag(this string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var values = value.Trim().Split(new char[2] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);


            StringBuilder sb = new StringBuilder();

            foreach (var item in values)
            {
                sb.Append(item);
                sb.Append("<br />");


            }

            return sb.ToString();
        }

        public static string[] SplitWithParagraphTags(this string value)
        {
            if (string.IsNullOrEmpty(value)) return new string[] { };
            var values = value.Trim().Split(new char[2] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);


            for (var i = 0; i < values.Length; i++)
            {
                values[i] = "<p>" + values[i] + "</p>";
            }

            return values;

        }

        public static string[] SplitWithParagraphTags(this string value, int length)
        {
            if (string.IsNullOrEmpty(value)) return new string[] { };
            var values = value.Trim().Split(new char[2] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            var sumString = "";
            int index = 0;
            IList<string> returnValues = new List<string>();

            for (var i = 0; i < values.Length; i++)
            {
                values[i] = "<p>" + values[i] + "</p>";
                if (sumString.Length < length && i != (values.Length - 1))
                {
                    sumString += values[i];
                    if (returnValues.Any())
                    {
                        returnValues[0] = sumString;
                    }
                    else
                    {
                        returnValues.Add(sumString);
                    }
                }
                else
                {
                    returnValues.Add(values[i]);
                }

            }

            return returnValues.ToArray();
        }

        public static string Base64Encode(string text)
        {
            var textBytes = Encoding.UTF8.GetBytes(text);
            return Convert.ToBase64String(textBytes);
        }
        public static string Base64Decode(string base64)
        {
            var base64Bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(base64Bytes);
        }

        public static string GenerateRandomAlphaNumeric(int length)
        {
            Random random = new Random();

            const string chars = "abcdefhijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ12345678";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        public static string ConvertParagraphToLineBreaks(this string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            value = value.Replace("<p>", "");
            value = value.Replace("</p>", "\n");

            return value.Trim();
        }


        public static string RemoveParagraph(this string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            value = value.Replace("<p>", "");
            value = value.Replace("</p>", "");

            return value.Trim();
        }

        public static string GetDocumentIcon(this string value)
        {

            if (value == ".pdf")
            {
                return "pdf";
            }
            else if (value == ".doc" || value == ".docx")
            {
                return "word";
            }
            else
            {
                return "excel";
            }

        }

        public static string RandomString(int length)
        {
            var random = new Random();

            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!$@#|_-";

            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static bool IsInteger(this string value)
        {
            int number = 0;
            return int.TryParse(value, out number);
        }

        public static string GetEmailDomain(this string email)
        {
            if (email.IndexOf('@') > -1)
            {
                return email.Split('@').ElementAt(1);
            }

            return email;
        }

        public static string GetInitials(this string value, bool useFirstAndLast = false)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var values = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (values.Count() == 1)
            {
                return value.First().ToString().ToUpper();
            }

            if (useFirstAndLast)
            {
                return values[0].First().ToString().ToUpper() + values[values.Length - 1].First().ToString().ToUpper();
            }

            return values[0].First().ToString().ToUpper() + values[1].First().ToString().ToUpper();
        }

        public static string FirstCharToUpper(this string input) => input switch
        {
            null => null,
            "" => null,
            _ => input.First().ToString().ToUpper() + input.Substring(1)
        };


        public static string ToCommaSeperatedString(this int[] value)
        {
            string final = string.Empty;

            for (int i = 0; i < value.Length; i++)
            {
                final += (i == 0 ? "" : ",") + value[i].ToString();

            }

            return final;
        }

        /// <summary>
        /// Strip off the HTML tags from given string
        /// </summary>
        /// <param name="html">HTML string</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string StripHtml(this string html)
        {
            string functionReturnValue = null;
            //Strips the HTML tags from strHTML

            var objRegExp = new Regex("<(.|\\n)+?>");
            string strOutput = string.Empty;

            //Replace all HTML tag matches with the empty string
            strOutput = objRegExp.Replace(html, "");

            //Replace all < and > with &lt; and &gt;
            strOutput = strOutput.Replace("<", "&lt;");
            strOutput = strOutput.Replace(">", "&gt;");

            functionReturnValue = strOutput;
            //Return the value of strOutput

            objRegExp = null;
            return functionReturnValue;
        }


        public static string ToFriendlyUrl(this string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            string returnString = text.Trim().ToLower();

            //a space, backslash or forward slash will be replaced with a hyphen
            Regex rgxReplaceWithHyphen = new Regex(@"[ \\/]");
            returnString = rgxReplaceWithHyphen.Replace(returnString, "-");

            //remove all non-latin chars, digits or hyphens
            Regex rgxRemove = new Regex("[^a-z0-9-]");
            returnString = rgxRemove.Replace(returnString, "");

            return returnString;
        }

        public static string ToUrl(this string text, bool hasSSL = false)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            if (text.StartsWith("http"))
                return text;

            return $"http{(hasSSL ? "s" : "")}://{text}";
        }

        /// <summary>
        /// Cut the string to nearest space from given length
        /// </summary>
        /// <param name="value">orignial string value</param>
        /// <param name="length">length</param>
        /// <returns></returns>
        public static string SubStringWithSpace(this string value, int length)
        {
            return value.SubStringWithSpace(length, string.Empty);
        }


        /// <summary>
        /// Cut the string to nearest space from given length
        /// </summary>
        /// <param name="value">orignial string value</param>
        /// <param name="length">length</param>
        /// <param name="suffix">value to suffix after the string for instance ...</param>
        /// <returns></returns>
        public static string SubStringWithSpace(this string value, int length, string suffix)
        {
            if (!string.IsNullOrEmpty(value))
            {
                //check if requested characters number is not more than lenght of text
                if (length > value.Length)
                {
                    return value;
                }

                string str_cut_only_text = value.Substring(0, length);

                int int_last_space_index = str_cut_only_text.LastIndexOf(" ");

                //if text has any spaces

                if (int_last_space_index > 0)
                {
                    string str_cut_text = "";
                    str_cut_text = str_cut_only_text.Substring(0, int_last_space_index);

                    if (str_cut_text[str_cut_text.Length - 1].ToString() == "." || str_cut_text[str_cut_text.Length - 1].ToString() == ",")
                    {
                        str_cut_text = str_cut_text.Substring(0, str_cut_text.Length - 1);
                    }

                    return str_cut_text + suffix;
                }
                else
                {
                    return str_cut_only_text + suffix;
                }

            }
            else
            {
                return string.Empty;
            }

        }


        public static bool IsNullOrEmptyOrWhiteSpace(this string value)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// returns empty string if it's null, empty or white space; otherwise returns the string
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ToEmptyWhenNull(this string s)
        {
            return s.IsNullOrEmptyOrWhiteSpace() ? "" : s;
        }

        public static string ToEmptyOrTrimmed(this string s)
        {
            if (s.IsNullOrEmptyOrWhiteSpace())
            {
                return "";
            }

            return s.Trim();
        }

        public static string ToNullWhenEmpty(this string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        /// replace every new line character with a <br /> tag
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string NewLineToHtmlBreak(this string text)
        {
            if (text.IsNullOrEmptyOrWhiteSpace())
            {
                return "";
            }

            text = text.Replace("\r\n", "<br />");
            text = text.Replace("\r", "<br />");
            text = text.Replace("\n", "<br />");

            return text;
        }

        /// <summary>
        /// two consequtive new line characters, or a single line character, will be replaced with a single <br /> tag
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string NewLinesToSingleHtmlBreak(this string text)
        {
            if (text.IsNullOrEmptyOrWhiteSpace())
            {
                return "";
            }

            //first, replace two consequtive new line characters with one
            text = text.Replace("\r\n\r\n", "\r\n");
            text = text.Replace("\r\r", "\r");
            text = text.Replace("\n\n", "\n");

            //and now, replace single new line characters with a <br /> tag
            text = text.NewLineToHtmlBreak();

            return text;
        }

        /// <summary>
        /// replace every new line character with a <p> tag. 
        /// </summary>
        /// <param name="text">Make sure the text is wrapped up with <p> and </p> tags
        /// <param name="wrapWithParagraph">Pass true if the text is NOT wrapped up with a <p> tag
        /// <returns></returns>
        public static string NewLineToHtmlParagraph(this string text, bool wrapWithParagraph = false)
        {
            if (text.IsNullOrEmptyOrWhiteSpace())
            {
                return "";
            }

            text = text.Replace("\r\n", "</p><p>");
            text = text.Replace("\r", "</p><p>");
            text = text.Replace("\n", "</p><p>");

            if (wrapWithParagraph)
            {
                text = $"<p>{text}</p>";
            }

            return text;
        }

        /// <summary>
        /// two consequtive new line characters, or a single line character, will be replaced with a single <p> tag
        /// </summary>
        /// <param name="text">Make sure the text is wrapped up with <p> and </p> tags
        /// <param name="wrapWithParagraph">Pass true if the text is NOT wrapped up with a <p> tag
        /// <returns></returns>
        public static string NewLinesToSingleHtmlParagraph(this string text, bool wrapWithParagraph = false)
        {
            if (text.IsNullOrEmptyOrWhiteSpace())
            {
                return "";
            }

            //first, replace two or more consequtive new line characters with one (via regex)
            //text = text.Replace("\r\n\r\n", "\r\n");
            //text = text.Replace("\r\r", "\r");
            //text = text.Replace("\n\n", "\n");

            bool crossNewLine = true;
            RegexOptions regexOption = crossNewLine ? RegexOptions.Singleline : RegexOptions.None;

            //https://www.rexegg.com/regex-quickstart.html
            //{2,} quantifier in the below regex means two or more subsequent occurrences, greedy & docile, see more at https://www.rexegg.com/regex-quantifiers.html#basics

            string pattern = @"(\r\n){2,}|([\r\n]){2,}";
            Regex regex = new Regex(pattern, regexOption);
            if (regex.IsMatch(text))
            {
                text = Regex.Replace(text, pattern, "\n", regexOption);
            }

            //and now, replace single new line characters with a <p> tag
            text = text.NewLineToHtmlParagraph(wrapWithParagraph);

            //finally, remove potential blank paragraphs
            text = text.Replace("<p></p>", "");

            return text;
        }

        /// <summary>
        /// two consequtive new line characters will be replaced with a <p> tag; otherwise with a <br /> tag
        /// </summary>
        /// <param name="text">Make sure the text is wrapped up with <p> and </p> tags
        /// <returns></returns>
        public static string NewLineToHtmlBreakOrParagraph(this string text)
        {
            if (text.IsNullOrEmptyOrWhiteSpace())
            {
                return "";
            }

            //first, replace two or more consequtive new line characters with a <p> tag
            text = text.Replace("\r\n\r\n", "</p><p>");
            text = text.Replace("\r\r", "</p><p>");
            text = text.Replace("\n\n", "</p><p>");


            //and now, replace single new line characters with a <br /> tag
            text = text.NewLineToHtmlBreak();

            return text;
        }


        /// <summary>
        /// trims a string of white characters and a potential prefix or a suffix. Prefix and suffix will be trimmed off white spaces first. 
        /// Example: use in a string join operation where items should first be trimmed off the separator, such as a comma or a semicolon
        /// </summary>
        /// <param name="value">string to sanitise</param>
        /// <param name="prefix">leading string or a character to remove</param>
        /// <param name="suffix">ending string or character to remove</param>
        /// <returns>a string trimmed of white characters and a prefix or a suffix</returns>
        public static string Trim(this string value, string prefix = "", string suffix = "")
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            //trim from white characters
            value = value.Trim();

            //sanitise prefix (trim)
            if (!string.IsNullOrEmpty(prefix))
            {
                prefix = prefix.Trim();

                if (value == prefix)
                {
                    return "";
                }

                if (value.StartsWith(prefix))
                {
                    value = value.Substring(prefix.Length);
                }
            }

            //sanitise suffix (trim)
            if (!string.IsNullOrEmpty(suffix))
            {
                suffix = suffix.Trim();

                if (value == suffix)
                {
                    return "";
                }

                if (value.EndsWith(suffix))
                {
                    value = value.Substring(0, value.Length - suffix.Length);
                }
            }

            return value;
        }

        /// <summary>
        /// turns placeholder markup to bold, italic, link to its HTML representation
        /// </summary>
        /// <param name="value"></param>
        /// <param name="crossNewLine">whether markup pattern can go through multiple lines</param>
        /// <param name="bold">indicate whether text wrapped with two stars should be considered as bold</param>
        /// <param name="italic">indicate whether text wrapped with one star should be considered as italic</param>
        /// <param name="link">indicate whether text placed inside [text](url) brackets should be considered as a link</param>
        /// <param name="linkTargetNew">indicate whether a link should open up in a new tab/window</param>
        /// <param name="bullets">indicate whether a hyphen at the start of line should create a bullet point</param>
        /// <param name="unnestBulletListFromParagraph">pass true if bullets are in use, and if a paragraph is the immediate outer wrapper. This will close the paragraph before opening bullet list and reopen after closing the list</param>
        /// <returns></returns>
        public static string PlaceholderMarkupToHtml(this string value,
            bool crossNewLine = false,
            bool bold = true,
            bool italic = true,
            bool link = true,
            bool linkTargetNew = true,
            bool bullets = true,
            bool unnestBulletListFromParagraph = true)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }
            value = value.Trim();

            RegexOptions regexOption = crossNewLine ? RegexOptions.Singleline : RegexOptions.None;

            //https://www.rexegg.com/regex-quickstart.html

            // @""  -> indicates a verbatim string >> means text is interpreted as literal
            // $""  -> indicates interpolation, and is a shortcut for string.Format(xxx, yyy) where variables or expressions could be contained within {} brackets
            // $@"" -> indicates a combination of verbatim and interpolation

            /*
            in Regex:
            (....) -> represents a group, multiple groups are allowed (see links example below)
            $1, $2 etc -> in Replace function refer to groups via 1-based index (0 means the whole found expression, 1, 2, etc mean each found group)
            
            //value = Regex.Replace(value, pattern, @"$1", regexOption);    >>    is the same as    >>
            //value = Regex.Replace(value, pattern, $"{m.Groups[1].Value}", regexOption);   == useful to use when we want to apply additional processing on the group

            *? -> a non-greedy match == the shortest possible, with 0+ occurrence. Useful if part of pattern is . (ie any character), that is otherwise greedy
            +? -> a non-greedy match == the shortest possible, with 1+ occurrence. Useful if part of pattern is . (ie any character), that is otherwise greedy
            \ -> literal - to be placed in front of any special character if we want it literally (eg * means 0+ occurrence, \* literally means a star character)
            * -> 0+ occurence of the previous character
            + -> 1+ occurence of the previous character
            {N} -> N quantifier == exactly N occurrence of the previous character; eg \*{2} means literally two stars
             */

            //example:
            //string patternLink = @"\[(.+?)\]\((.+?)\)";   //brackets - link
            //string str = "For more info, [click here](https://www.linkgoeshere.com) or [contact us](https://contact.us/form) and [I/we] will (come) back to you.";
            //Regex regex = new Regex(patternLink, regexOption);
            //var matches = regex.Matches(str);
            //foreach (var match in matches)
            //{
            //    var x = match;
            //}
            //str = Regex.Replace(str, patternLink, @"<a href=""$2"">$1</a>", regexOption);

            if (bold)
            {
                string pattern = @"\*{2}(.+?)\*{2}";    //two stars == bold
                Regex regex = new Regex(pattern, regexOption);
                if (regex.IsMatch(value))
                {
                    value = Regex.Replace(value, pattern, @"<b>$1</b>", regexOption);
                }
            }

            if (italic)
            {
                string pattern = @"\*(.+?)\*";        //one star == italic
                Regex regex = new Regex(pattern, regexOption);
                if (regex.IsMatch(value))
                {
                    value = Regex.Replace(value, pattern, @"<i>$1</i>", regexOption);
                }
            }

            if (link)
            {
                string pattern = @"\[(.+?)\]\((.+?)\)";   //brackets - link
                Regex regex = new Regex(pattern, regexOption);
                if (regex.IsMatch(value))
                {
                    //can't use $1 syntax for the group, because we need to do additional processing on it
                    //value = Regex.Replace(value, pattern, $@"<a href=""$2""{(linkTargetNew ? " target=\"_blank\"" : "")}>$1</a>", regexOption);
                    value = Regex.Replace(value, pattern, m => $@"<a href=""{m.Groups[2].Value.ToUrl()}""{(linkTargetNew ? " target=\"_blank\"" : "")}>{m.Groups[1].Value}</a>", regexOption);
                }
            }

            if (bullets)
            {
                //first, replace RNs with just Ns, or Rs with Ns
                value = value.Replace("\r\n", "\n").Replace("\r", "\n");

                //value = "Some text \n- bul1\n- bul2\n-End of S1.\n";
                //value += "Some text \n-bul3\n-bul4\nEnd of S2.";

                /*
                Lookaround                      Example	                Sample Match
                (?=…)	Positive lookahead	    (?=\d{10})\d{5}	        01234 in 0123456789
                (?<=…)	Positive lookbehind	    (?<=\d)cat	            cat in 1cat
                (?!…)	Negative lookahead	    (?!theatre)the\w+	    theme
                (?<!…)	Negative lookbehind	    \w{3}(?<!mon)ster	    Munster
                 */

                // lookahead -> means to look ahead (>>) further in the string, what's there to the right after the current string
                // lookbehind <- means to look behind (<<) back in the string, what's there to the left before the current string

                // the reason to use lookarounds: what we look at, will NOT be part of the match

                // 1) START + hyphen + ... + N   == <ul><li> ... </li></ul>
                // 2)     N + hyphen + ... + N   == <ul><li> ... </li></ul>
                // 3)     N + hyphen + ... + END == <ul><li> ... </li></ul>

                //^ and $ mean start/end of the whole string (in the singleline mode, they would mean start/end of a line)
                //string pattern = @"^\-(.+?)$";        //new line N + hyphen + ... + N == <ul><li> ... </li></ul>

                //this covers 2) and 3), not 1), so let's cheat - if the whole string starts with a hyphen, we'll add N to the very beginning of it
                if (value.StartsWith("-"))
                {
                    value = "\n" + value;
                }



                //we'll match them line by line - so we'll match the new line char, a hyphen, text of bullet and look ahead if there's another new line char (or end of string) after this line
                //we'll replace the starting new line, hyphen and text (but NOT the subsequent new line, that has to be matched as a new matched line)
                string pattern = @"\n\-(.+?)(?=\n|$)";

                Regex regex = new Regex(pattern, regexOption);
                if (regex.IsMatch(value))
                {
                    var matches = regex.Matches(value);

                    //add bullet points (li's) for now including the ul wrappers
                    value = Regex.Replace(value, pattern, @"<ul><li>$1</li></ul>", regexOption);

                    /*
                    remove repeated subsequent ul wrappers on each li item, so that instead of:
                    
                            <ul><li> ... </li></ul>
                            <ul><li> ... </li></ul>
                            <ul><li> ... </li></ul>
                    
                    to just end up with:
                    
                            <ul><li> ... </li>
                                <li> ... </li>
                                <li> ... </li></ul>
                    */
                    value = value.Replace("</ul><ul>", "");

                    //I think it's ok to also remove the new line char after the ending ul wrapper tag
                    value = value.Replace("</ul>\n", "</ul>");

                    //lastly... if a paragraph tag is an outer wrapper, then we need to close it before opening bullet list and reopen after closing the list</param>
                    //HTML spec doesn't allow nesting ULs or OLs inside of a P tag
                    if (unnestBulletListFromParagraph)
                    {
                        value = value.Replace("<ul>", "</p><ul>");
                        value = value.Replace("</ul>", "</ul><p>");
                    }
                }
            }

            return value;
        }

        public static string AddFilenameSuffix(this string filename, string suffix)
        {
            var filenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            var extension = Path.GetExtension(filename);
            return filenameWithoutExtension + suffix + extension;
        }

        public static string Left(this string str, int maxLen = 0, bool sanitiseYoutubeFormat = false)
        {
            if (str == null)
                return "";

            str = str.Trim();

            if (sanitiseYoutubeFormat)
            {
                str = str.Replace("<", "«").Replace(">", "»");
            }

            if (maxLen > 0)
            {
                maxLen = Math.Min(maxLen, str.Length);
                str = str.Substring(0, maxLen);
            }

            return str;
        }


        /// <summary>
        /// gets leftmost substring up to the first space
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GetForename(this string value)
        {
            value = value.ToEmptyOrTrimmed();
            if (value == "") return "";

            if (!value.Contains(" "))
            {
                return value;
            }

            int spacePos = value.IndexOf(' ');

            return value.Substring(0, spacePos);
        }

        /// <summary>
        /// gets the right part of the string starting after the first space (in case there are multiple of them)
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GetSurname(this string value)
        {
            value = value.ToEmptyOrTrimmed();
            if (value == "") return "";

            if (!value.Contains(" "))
            {
                return "";
            }

            int spacePos = value.IndexOf(' ');
            return value.Substring(spacePos + 1);
        }
    }
}
