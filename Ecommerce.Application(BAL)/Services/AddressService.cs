using Ecommerce.Application_BAL_.DTOs.Addresses;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;

namespace Ecommerce.Application_BAL_.Services;

public class AddressService : IAddressService
{
    private readonly IAddressRepository _addressRepository;

    private readonly IUserRepository _userRepository;

    public AddressService(IAddressRepository addressRepository, IUserRepository userRepository)
    {
        _addressRepository = addressRepository
            ?? throw new ArgumentNullException(nameof(addressRepository));

        _userRepository = userRepository
            ?? throw new ArgumentNullException(nameof(userRepository));
    }
    public async Task<AddressDto?> GetByIdAsync(long addressId, long userId)
    {
        ValidateIds(addressId, userId);

        var address = await _addressRepository.GetByIdAsync(addressId);

        if (address is null || address.UserId != userId)
            return null;

        return MapToDto(address);
    }

    public async Task<IEnumerable<AddressDto>> GetByUserIdAsync(long userId)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));

        var addresses = await _addressRepository.GetByUserIdAsync(userId);

        return addresses.Select(MapToDto);
    }

    public async Task<AddressDto> CreateAsync(long userId, CreateAddressDto dto)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));

        ArgumentNullException.ThrowIfNull(dto);

        ValidateAddressData(
            dto.Label,
            dto.City,
            dto.Street,
            dto.Phone,
            dto.BuildingDetails);

        var user = await _userRepository.GetByIdAsync(userId);

        if (user is null)
        {
            throw new KeyNotFoundException("User was not found.");
        }

        var address = new Address
        {
            UserId = userId,
            Label = NormalizeOptionalText(dto.Label),
            City = dto.City.Trim(),
            Street = dto.Street.Trim(),
            Phone = dto.Phone.Trim(),
            BuildingDetails = NormalizeOptionalText(dto.BuildingDetails),
            IsDefault = dto.IsDefault
        };

        long addressId = await _addressRepository.CreateAsync(address);

        var createdAddress = await _addressRepository.GetByIdAsync(addressId);

        if (createdAddress is null)
        {
            throw new InvalidOperationException(
                "The address was created but could not be retrieved.");
        }

        return MapToDto(createdAddress);
    }

    public async Task<bool> UpdateAsync(long addressId,long userId,UpdateAddressDto dto)
    {
        ValidateIds(addressId, userId);
        ArgumentNullException.ThrowIfNull(dto);

        ValidateAddressData(
            dto.Label,
            dto.City,
            dto.Street,
            dto.Phone,
            dto.BuildingDetails);

        var existingAddress =
            await _addressRepository.GetByIdAsync(addressId);

        if (existingAddress is null || existingAddress.UserId != userId)
            return false;

        existingAddress.Label = NormalizeOptionalText(dto.Label);
        existingAddress.City = dto.City.Trim();
        existingAddress.Street = dto.Street.Trim();
        existingAddress.Phone = dto.Phone.Trim();
        existingAddress.BuildingDetails =
            NormalizeOptionalText(dto.BuildingDetails);

        return await _addressRepository.UpdateAsync(existingAddress);
    }

    public async Task<bool> DeleteAsync(long addressId, long userId)
    {
        ValidateIds(addressId, userId);

        return await _addressRepository.DeleteAsync(addressId, userId);
    }

    public async Task<bool> SetDefaultAddressAsync(long addressId, long userId)
    {
        ValidateIds(addressId, userId);

        var existingAddress = await _addressRepository.GetByIdAsync(addressId);

        if (existingAddress is null || existingAddress.UserId != userId)
            return false;

        if (existingAddress.IsDefault)
            return true; 

        return await _addressRepository.SetDefaultAddressAsync(addressId, userId);
    }

    private static void ValidateIds(long addressId, long userId)
    {
        if (addressId <= 0)
            throw new ArgumentOutOfRangeException(nameof(addressId));

        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));
    }

    private static void ValidateAddressData(
        string? label,
        string city,
        string street,
        string phone,
        string? buildingDetails)
    {
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City is required.");

        if (city.Trim().Length > 100)
            throw new ArgumentException(
                "City cannot exceed 100 characters.");

        if (string.IsNullOrWhiteSpace(street))
            throw new ArgumentException("Street is required.");

        if (street.Trim().Length > 200)
            throw new ArgumentException(
                "Street cannot exceed 200 characters.");

        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Phone is required.");

        if (phone.Trim().Length > 30)
            throw new ArgumentException(
                "Phone cannot exceed 30 characters.");

        if (label?.Trim().Length > 50)
            throw new ArgumentException(
                "Label cannot exceed 50 characters.");

        if (buildingDetails?.Trim().Length > 200)
            throw new ArgumentException(
                "Building details cannot exceed 200 characters.");
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static AddressDto MapToDto(Address address)
    {
        return new AddressDto
        {
            AddressId = address.AddressId,
            UserId = address.UserId,
            Label = address.Label,
            City = address.City,
            Street = address.Street,
            Phone = address.Phone,
            BuildingDetails = address.BuildingDetails,
            IsDefault = address.IsDefault
        };
    }
}