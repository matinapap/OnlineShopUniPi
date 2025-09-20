document.addEventListener("DOMContentLoaded", function () {
    const maleClothes = [
        "Shirts",
        "T-shirts / Tops",
        "Sweaters and Hoodies",
        "Jackets / Coats",
        "Jeans and Pants",
        "Other"
    ];

    const femaleClothes = [
        "Dresses and bodysuits",
        "T-shirts / Tops",
        "Jackets / Coats",
        "Beachwear / Swimwear",
        "Shirts and blouses",
        "Pants / Trousers",
        "Sweaters / Jumpers",
        "Jeans",
        "Hoodies / Sweatshirts",
        "Skirts and shorts",
        "Other"
    ];

    const genderSelect = document.getElementById("GenderSelect");
    const categorySelect = document.getElementById("CategorySelect");

    if (!genderSelect || !categorySelect) {
        console.error("Δεν βρέθηκαν τα στοιχεία GenderSelect ή CategorySelect");
        return;
    }

    genderSelect.addEventListener("change", function () {
        const gender = this.value;
        categorySelect.innerHTML = '<option value="">-- Επιλέξτε κατηγορία --</option>';

        let clothesList = [];

        if (gender === "Men") {
            clothesList = maleClothes;
        } else if (gender === "Women") {
            clothesList = femaleClothes;
        }

        if (clothesList.length > 0) {
            clothesList.forEach(cat => {
                const option = document.createElement("option");
                option.value = cat;
                option.text = cat;
                categorySelect.appendChild(option);
            });
            categorySelect.disabled = false;
        } else {
            categorySelect.disabled = true;
        }
    });
});
