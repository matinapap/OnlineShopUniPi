using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace OnlineShopUniPi.Models;

[Index("Email", Name = "UQ__Users__AB6E61642D3D7614", IsUnique = true)]
[Index(nameof(Username), Name = "UQ__Users__Username", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("user_id")]
    public int UserId { get; set; }

    [Column("first_name")]
    [StringLength(100)]
    [Unicode(false)]
    public string FirstName { get; set; } = null!;

    [Column("last_name")]
    [StringLength(100)]
    [Unicode(false)]
    public string LastName { get; set; } = null!;

    [Column("email")]
    [StringLength(255)]
    [Unicode(false)]
    public string Email { get; set; } = null!;

    [Column("password_hash")]
    [StringLength(255)]
    [Unicode(false)]
    public string PasswordHash { get; set; } = null!;

    [Column("phone_number")]
    [StringLength(15)]
    [Unicode(false)]
    public string? PhoneNumber { get; set; }

    [Column("profile_picture_url")]
    [StringLength(255)]
    [Unicode(false)]
    public string? ProfilePictureUrl { get; set; }

    [Column("address")]
    [StringLength(255)]
    [Unicode(false)]
    public string? Address { get; set; }

    [Column("city")]
    [StringLength(100)]
    [Unicode(false)]
    public string? City { get; set; }

    [Column("country")]
    [StringLength(100)]
    [Unicode(false)]
    public string? Country { get; set; }

    [Column("registration_date", TypeName = "datetime")]
    public DateTime? RegistrationDate { get; set; }

    [Column("username")]
    [StringLength(100)]
    [Unicode(false)]
    public string Username { get; set; } = null!;

    [Column("role")]
    [StringLength(10)]
    [Unicode(false)]
    public string Role { get; set; } = null!;

    [InverseProperty("User")]
    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    [InverseProperty("User")]
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    [InverseProperty("User")]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

}
