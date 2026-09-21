using SeOne.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SeOne.Application.Interfaces;

public interface IBlogService
{
    Task<List<BlogPostDto>> GetPublishedPostsAsync(string language);
    Task<BlogPostDto?> GetPublishedPostByIdAsync(Guid id);
    Task<BlogPostDto> CreateAsync(CreateBlogPostRequest request);
    Task<BlogPostDto?> UpdateAsync(Guid id, UpdateBlogPostRequest request);
    Task<bool> DeleteAsync(Guid id);
}
