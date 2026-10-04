using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace OnlineShopUniPi.Models;

public partial class ProductImage
{
    [Key]
    [Column("image_id")]
    public int ImageId { get; set; }

    [Column("product_id")]
    public int ProductId { get; set; }

    [Column("image_url")]
    [StringLength(255)]
    [Unicode(false)]
    public string ImageUrl { get; set; } = null!;

    [Column("is_main_image")]
    public bool? IsMainImage { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("ProductImages")]
    public virtual Product Product { get; set; } = null!;
}
