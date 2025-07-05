using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace OnlineShopUniPi.Models;

public partial class SellerReview
{
    [Key]
    [Column("review_id")]
    public int ReviewId { get; set; }

    [Column("product_id")]
    public int ProductId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("rating")]
    public int Rating { get; set; }

    [Column("review_text", TypeName = "text")]
    public string? ReviewText { get; set; }

    [Column("review_date", TypeName = "datetime")]
    public DateTime? ReviewDate { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("SellerReviews")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("SellerReviews")]
    public virtual User User { get; set; } = null!;
}
