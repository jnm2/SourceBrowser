﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibGit2Sharp;
using Microsoft.SourceBrowser.MEF;

namespace GitGlyph
{
    public class GitBlameVisitor : ITextVisitor
    {
        private Repository Repository { get; set; }
        private string Root { get; set; }
        private ILog Logger { get; set; }

        public GitBlameVisitor(Repository r, ILog logger)
        {
            Repository = r;
            Logger = logger;
            Root = System.IO.Path.GetFullPath(Repository.Info.WorkingDirectory);
        }

        public string Visit(string text, IReadOnlyDictionary<string, string> context)
        {
            var path = System.IO.Path.GetFullPath(context[ContextKeys.FilePath]);
            var zeroIndexedLineNumber = Int32.Parse(context[ContextKeys.LineNumber]) - 1;
            var blame = GetBlame(path)?.FirstOrDefault(bh => bh.FinalStartLineNumber == zeroIndexedLineNumber);
            if (blame != null)
            {
                var author = blame.FinalCommit.Author;
                return string.Format("<a href='mailto:{0}?subject={3}&body={2}'>{1}</a>", author.Email, author.Name, Uri.EscapeDataString(string.Join("\n\n", new[] {
                    string.Empty,
                    "--------------------------------------------------------------------------------",
                    FormatContext(context),
                    FormatCommit(blame.FinalCommit)
                })), "Email from SourceBrowser");
            }

            return null;
        }

        private string FormatContext(IReadOnlyDictionary<string, string> context)
        {
            return string.Format("File: {0}\nLine: {1}", MakeRelativeToRepository(context[ContextKeys.FilePath]), context[ContextKeys.LineNumber]);
        }

        private string ShortenObjectId(GitObject o)
        {
            return Repository.ObjectDatabase.ShortenObjectId(o);
        }

        private string FormatCommit(Commit c)
        {
            const string RFC2822Format = "ddd dd MMM HH:mm:ss yyyy K";
            StringBuilder result = new StringBuilder();

            result.AppendFormat("commit {0}", c.Id).AppendLine();

            if (c.Parents.Count() > 1)
            {
                result.AppendFormat(
                    "Merge: {0}",
                    string.Join(" ", c.Parents.Select(ShortenObjectId).ToArray())
                ).AppendLine();
            }

            result.AppendFormat("Author: {0} <{1}>", c.Author.Name, c.Author.Email).AppendLine();
            result.AppendFormat("Date:   {0}", c.Author.When.ToString(RFC2822Format, System.Globalization.CultureInfo.InvariantCulture)).AppendLine();
            result.AppendLine();
            result.AppendLine(c.Message);
            result.AppendLine();

            return result.ToString();
        }

        /// <summary>
        /// Cache used to memoize the GetBlame method.
        /// </summary>
        private readonly Dictionary<string, IEnumerable<BlameHunk>> getBlameResultCache = new Dictionary<string, IEnumerable<BlameHunk>>();
        private IEnumerable<BlameHunk> GetBlame(string path)
        {
            if (!getBlameResultCache.TryGetValue(path, out IEnumerable<BlameHunk> result))
            {
                try
                {
                    if (path.StartsWith(Root))
                    {
                        var relativePath = MakeRelativeToRepository(path);
                        if (!Repository.Ignore.IsPathIgnored(relativePath))
                        {
                            result = Repository.Blame(relativePath);
                        }
                    }
                    else
                    {
                        result = new BlameHunk[0];
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info("Couldn't blame " + path, ex);
                    result = new BlameHunk[0];
                }
                getBlameResultCache.Add(path, result);
            }
            return result;
        }

        private string MakeRelativeToRepository(string path)
        {
            return path.Replace(Root, string.Empty);
        }
    }
}
