using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface IWishlistService
{
    Task<List<WishlistItemDto>> GetAsync(Guid studentId);

    Task<WishlistItemDto?> AddAsync(
        Guid studentId,
        Guid courseId);

    Task<bool> RemoveAsync(
        Guid studentId,
        Guid courseId);

    Task<bool> ExistsAsync(
        Guid studentId,
        Guid courseId);
}