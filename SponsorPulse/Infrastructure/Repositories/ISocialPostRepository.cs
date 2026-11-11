using SponsorPulse.Domain.Models;
using SponsorPulse.Infrastructure.Persistence;

namespace SponsorPulse.Infrastructure.Repositories;

public interface ISocialPostRepository
{
    Task SavePostsAsync(
        IEnumerable<SocialPost> posts,
        CancellationToken cancellationToken = default
    );
    Task<string?> GetRawJsonForLlmAsync(Guid postId, CancellationToken cancellationToken = default);

    // static void MapPostEntityFromPost(SocialPost post, SocialPostEntity entity);

    // static SocialPost MapPostEntitytoPost(SocialPostEntity entity);
    Task<List<SocialPost>> GetPostsByEventIdAsync(Guid eventId, CancellationToken token = default);

    Task<bool> HasAlreadyPosts(Guid eventId, CancellationToken cancellationToken = default);

    Task<int> DeleteOldPostByEventId(Guid eventId, CancellationToken cancellationToken = default);
}
