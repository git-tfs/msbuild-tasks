using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Octokit;

namespace GitTfsTasks
{
    /// <summary>
    /// Create a release on github.com.
    /// </summary>
    /// <example>
    /// <code><![CDATA[
    /// <ItemGroup>
    ///   <ReleaseFiles Include="MyAwesomeProject-v0.1.0.zip" />
    ///   <ReleaseFiles Include="MyAwesomeReleaseNotes.md">
    ///     <ContentType>text/plain</ContentType>
    ///   </ReleaseFiles>
    /// </ItemGroup>
    /// <ItemGroup>
    ///   <ReleaseNotesFile Include="MyAwesomeReleaseNotes.md" />
    /// </ItemGroup>
    /// <Target Name="Release">
    ///   <CreateRelease Repository="owner/repo" OauthToken="$(GitHubAuthToken)" TagName="v0.1.0" Files="@(ReleaseFiles)" ReleaseNotesFile="$(ReleaseNotesFile)" />
    /// </Target>
    /// ]]></code>
    /// </example>
    public class CreateRelease : Task
    {
        [Required]
        public string Repository { get; set; }

        [Required]
        public string OauthToken { get; set; }

        [Required]
        public string TagName { get; set; }

        public ITaskItem[] Files { get; set; }

        public string ReleaseNotesFile { get; set; }

        [Output]
        public ITaskItem[] UploadedAssets { get; private set; }

        [Output]
        public int IdRelease { get; private set; }

        private string Owner { get { return Repository.Split('/')[0]; } }

        private string RepositoryName { get { return Repository.Split('/')[1]; } }

        private ICredentialStore CredentialStore { get { return new InPlaceCredentialStore(OauthToken); } }

        class InPlaceCredentialStore : ICredentialStore
        {
            string _token;
            public InPlaceCredentialStore(string token)
            {
                _token = token;
            }
            public async System.Threading.Tasks.Task<Credentials> GetCredentials()
            {
                return new Credentials(_token);
            }
        }

        public override bool Execute()
        {
            var client = new GitHubClient(new ProductHeaderValue("GitTfsTasks"), CredentialStore).Release;
            var release = client.CreateRelease(Owner, RepositoryName, BuildReleaseData()).Result;
            IdRelease = release.Id;
            Log.LogMessage("Created Release {0} at {1}", release.TagName, release.HtmlUrl);
            if (Files != null && Files.Length != 0)
            {
                UploadedAssets = UploadAll(client, release, Files);
                foreach (var item in UploadedAssets) Log.LogMessage("Uploaded {0}", item.ItemSpec);
            }
            return true;
        }

        private ITaskItem[] UploadAll(IReleasesClient client, Release release, IEnumerable<ITaskItem> items)
        {
            return items.Select(item => { Log.LogMessage("Uploading {0}...", item.ItemSpec); return Upload(client, release, item).Result; }).ToArray();
        }

        private async System.Threading.Tasks.Task<ITaskItem> Upload(IReleasesClient client, Release release, ITaskItem sourceItem)
        {
            var uploadedAsset = await client.UploadAsset(release, BuildAssetUpload(sourceItem));
            return TaskItemFor(release, uploadedAsset);
        }

        private ReleaseUpdate BuildReleaseData()
        {
            var release = new ReleaseUpdate(TagName);
            if (ReleaseNotesFile != null)
            {
                release.Body = File.ReadAllText(ReleaseNotesFile);
            }
            return release;
        }

        private ReleaseAssetUpload BuildAssetUpload(ITaskItem item)
        {
            var data = new ReleaseAssetUpload();
            data.ContentType = item.GetMetadata("ContentType");
            if (data.ContentType == null) data.ContentType = "application/octet-stream";
            data.FileName = Path.GetFileName(item.ItemSpec);
            data.RawData = File.OpenRead(item.ItemSpec);
            return data;
        }

        private ITaskItem TaskItemFor(Release release, ReleaseAsset asset)
        {
            var item = new TaskItem();
            // I don't think there's a way, via the API, to get something like this:
            // https://github.com/git-tfs/msbuild-tasks/releases/download/v0.0.9/GitTfsTasks-0.0.9.zip
            item.ItemSpec = "https://github.com/" + Repository + "/releases/download/" + TagName + "/" + asset.Name;
            item.MaybeSetMetadata("ContentType", asset.ContentType);
            item.MaybeSetMetadata("Id", asset.Id.ToString());
            item.MaybeSetMetadata("Label", asset.Label);
            item.MaybeSetMetadata("Name", asset.Name);
            item.MaybeSetMetadata("State", asset.State);
            return item;
        }
    }
}