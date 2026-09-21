using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SeOne.Application.DTOs;

namespace SeOne.Application.Interfaces;

public interface IBookingService
{
    Task<BookingDto?> CreateAsync(Guid studentId, CreateBookingRequest request);

    Task<List<BookingDto>> GetMyBookingsAsync(Guid studentId);

    Task<bool> CancelMyBookingAsync(Guid bookingId, Guid studentId);

    // Teacher operations
    Task<List<BookingDto>> GetTeacherBookingsAsync(Guid teacherId);

    Task<bool> ConfirmBookingAsync(Guid bookingId, Guid teacherId);

    Task<bool> CancelBookingByTeacherAsync(Guid bookingId, Guid teacherId);

    Task<bool> CompleteBookingAsync(Guid bookingId, Guid teacherId);
}
