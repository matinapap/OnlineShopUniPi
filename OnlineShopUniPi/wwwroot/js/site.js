// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// ####################### Login/Signup Toggle #######################

document.addEventListener('DOMContentLoaded', function () {
    const loginBtn = document.getElementById('loginBtn');
    const signupBtn = document.getElementById('signupBtn');
    const loginForm = document.getElementById('loginForm');
    const signupForm = document.getElementById('signupForm');

    // Set Login as default active
    loginBtn.classList.add('active');
    loginForm.classList.add('active');
    signupBtn.classList.remove('active');
    signupForm.classList.remove('active');

    loginBtn.addEventListener('click', function () {
        loginBtn.classList.add('active');
        signupBtn.classList.remove('active');
        loginForm.classList.add('active');
        signupForm.classList.remove('active');
    });

    signupBtn.addEventListener('click', function () {
        signupBtn.classList.add('active');
        loginBtn.classList.remove('active');
        signupForm.classList.add('active');
        loginForm.classList.remove('active');
    });

    // Initialize country/city select when page loads
    initializeCountryCitySelect();
});

// fetches countries and cities from API and populates the selects
async function initializeCountryCitySelect() {
    const countrySelect = document.getElementById('countrySelect');
    const citySelect = document.getElementById('citySelect');

    if (!countrySelect || !citySelect) return;

    try {
        // Load countries
        const countriesResponse = await fetch("https://countriesnow.space/api/v0.1/countries/positions");
        const countriesData = await countriesResponse.json();

        countriesData.data.forEach(country => {
            const option = new Option(country.name, country.name);
            countrySelect.add(option);
        });

        // Load cities for initial selected country (if any)
        if (countrySelect.value) {
            await loadCities(countrySelect.value, citySelect);
        }

        // Handle country change
        countrySelect.addEventListener("change", async () => {
            await loadCities(countrySelect.value, citySelect);
        });

    } catch (error) {
        console.error("Error initializing country/city select:", error);
    }
}

// loads cities for a given country and populates the city select
async function loadCities(country, citySelect) {
    if (!country || !citySelect) return;

    try {
        citySelect.innerHTML = '<option value="">Loading cities...</option>';

        const response = await fetch("https://countriesnow.space/api/v0.1/countries/cities", {
            method: "POST",
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ country })
        });

        const data = await response.json();
        citySelect.innerHTML = '';

        if (data.data && data.data.length > 0) {
            data.data.forEach(city => {
                citySelect.add(new Option(city, city));
            });
        } else {
            citySelect.add(new Option('No cities found', ''));
        }
    } catch (error) {
        console.error("Error loading cities:", error);
        citySelect.innerHTML = '';
        citySelect.add(new Option('Error loading cities', ''));
    }
}

// using Regex to validate fields
function validateFields() {
    const firstName = document.getElementById('FirstName').value.trim();
    const lastName = document.getElementById('LastName').value.trim();
    const username = document.getElementById('Username').value.trim();
    const email = document.getElementById('Email').value.trim();
    const phoneNumber = document.getElementById('PhoneNumber').value.trim();
    const password = document.getElementById('PasswordHash').value;
    const confirmPassword = document.getElementById('ConfirmPassword').value;

    const nameRegex = /^[A-Za-z\s]+$/;
    const usernameRegex = /^[a-z._]+$/;  
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    const phoneRegex = /^\d+$/;
    const passwordRegex = /^(?=.*[A-Z])(?=.*\d).{8,}$/;

    if (!nameRegex.test(firstName)) {
        alert("Το πεδίο 'First Name' πρέπει να περιέχει μόνο λατινικούς χαρακτήρες και κενά.");
        return false;
    }

    if (!nameRegex.test(lastName)) {
        alert("Το πεδίο 'Last Name' πρέπει να περιέχει μόνο λατινικούς χαρακτήρες και κενά.");
        return false;
    }

    if (!usernameRegex.test(username)) {
        alert("Το πεδίο 'Username' πρέπει να περιέχει μόνο μικρά λατινικά γράμματα, παύλα (_) και τελεία (.).");
        return false;
    }

    if (!emailRegex.test(email)) {
        alert("Παρακαλώ εισάγετε ένα έγκυρο email.");
        return false;
    }

    if (!phoneRegex.test(phoneNumber)) {
        alert("Το πεδίο 'Phone Number' πρέπει να περιέχει μόνο αριθμούς.");
        return false;
    }

    if (!passwordRegex.test(password)) {
        alert("Ο κωδικός πρέπει να περιέχει τουλάχιστον 8 χαρακτήρες, έναν κεφαλαίο και έναν αριθμό.");
        return false;
    }

    if (password !== confirmPassword) {
        alert("Οι κωδικοί δεν ταιριάζουν.");
        return false;
    }

    return true;
}

// ####################### Retrieves the ASP.NET Core anti-forgery token from the hidden form #######################

function getAntiForgeryToken() {
    const tokenInput = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]');
    return tokenInput ? tokenInput.value : '';
}

// ####################### Home/Layout Favourites #######################

function toggleHeart(button, event) {
    event.preventDefault();

    const icon = button.querySelector('i');
    const productId = parseInt(button.getAttribute('data-product-id'));

    fetch('/Products/Favorites', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': getAntiForgeryToken()
        },
        body: JSON.stringify({ productId: productId })
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                if (icon.classList.contains('bi-heart')) {
                    icon.classList.remove('bi-heart');
                    icon.classList.add('bi-heart-fill');
                    icon.style.color = 'red';
                } else {
                    icon.classList.remove('bi-heart-fill');
                    icon.classList.add('bi-heart');
                    icon.style.color = '';
                }
            } else {
                alert('Προέκυψε πρόβλημα.');
            }
        })
        .catch(() => alert('Προέκυψε σφάλμα στο αίτημα.'));
}


// ####################### Purchases Handling Dropdown #######################

document.getElementById('orderFilter')?.addEventListener('change', function () {
    const filter = this.value;
    window.location.href = `/Orders/MyPurchases?filter=${filter}`;
});

// ####################### Orders Handling Dropdown #######################

document.getElementById('orderFilter')?.addEventListener('change', function () {
    const filter = this.value;
    window.location.href = `/Orders/MyOrders?filter=${filter}`;
});

document.querySelectorAll('.order-status-select').forEach(function (select) {
    select.addEventListener('change', async function () {
        const orderId = parseInt(this.dataset.orderId);
        const status = this.value;

        try {
            const response = await fetch('@Url.Action("UpdateOrderStatus", "Orders")', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
                },
                body: JSON.stringify({ OrderId: orderId, Status: status })
            });

            const data = await response.json();
            if (data.success) {
                location.reload();
            } else {
                alert('Error updating order: ' + (data.message || ''));
            }
        } catch (err) {
            console.error(err);
            alert('Error updating order.');
        }
    });
});

// ####################### Favorites #######################

document.addEventListener('DOMContentLoaded', function () {
    const isAuthenticated = document.body.dataset.authenticated === "true";

    document.querySelectorAll('.favorite-toggle').forEach(button => {
        button.addEventListener('click', async function () {
            if (!isAuthenticated) {
                window.location.href = '/Users/LoginSignup';
                return;
            }

            const productId = parseInt(this.dataset.productId);
            const icon = this.querySelector('i');

            try {
                const response = await fetch('/Products/Favorites', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                    },
                    body: JSON.stringify({ ProductId: productId })
                });

                if (response.ok) {
                    // Toggle καρδιάς
                    icon.classList.toggle('bi-heart');
                    icon.classList.toggle('bi-heart-fill');

                    // Αν είμαστε στη σελίδα Favorites και κάναμε unfavorite, αφαίρεσε το προϊόν
                    if (window.location.pathname.toLowerCase().includes('/products/favorites')) {
                        const cardCol = this.closest('.col');
                        cardCol.remove();

                        // Έλεγχος αν έμειναν άλλα προϊόντα
                        const remaining = document.querySelectorAll('.favorite-toggle').length;
                        if (remaining === 0) {
                            const container = document.querySelector('.section .row');
                            container.innerHTML = '<p class="text-muted text-center">Δεν έχετε προσθέσει προϊόντα στα αγαπημένα.</p>';
                        }
                    }
                } else {
                    console.error("Η ενέργεια απέτυχε.");
                }
            } catch (error) {
                console.error("Σφάλμα κατά την αποστολή:", error);
            }
        });
    });
});

// ####################### Clothing Categories #######################

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

function populateCategories(selectedGender, selectedCategory) {
    categorySelect.innerHTML = '<option value="">-- Επιλέξτε κατηγορία --</option>';
    let clothesList = [];

    if (selectedGender === "Men") {
        clothesList = maleClothes;
    } else if (selectedGender === "Women") {
        clothesList = femaleClothes;
    }

    if (clothesList.length > 0) {
        clothesList.forEach(cat => {
            const option = document.createElement("option");
            option.value = cat;
            option.text = cat;
            if (cat === selectedCategory) {
                option.selected = true;
            }
            categorySelect.appendChild(option);
        });
        categorySelect.disabled = false;
    } else {
        categorySelect.disabled = true;
    }
}

document.addEventListener("DOMContentLoaded", function () {
    const currentGender = "@Model.Gender";
    const currentCategory = "@Model.Category";
    if (currentGender) {
        populateCategories(currentGender, currentCategory);
    }
});

genderSelect.addEventListener("change", function () {
    populateCategories(this.value, "");
});

// ####################### Details Quantity Buttons #######################

const decreaseBtn = document.getElementById("decreaseBtn");
const increaseBtn = document.getElementById("increaseBtn");
const quantityInput = document.getElementById("quantityInput");
const maxQuantity = parseInt(quantityInput.max);

    decreaseBtn.addEventListener("click", () => {
    let val = parseInt(quantityInput.value);
        if (val > 1) quantityInput.value = val - 1;
    });

    increaseBtn.addEventListener("click", () => {
    let val = parseInt(quantityInput.value);
if (val < maxQuantity) quantityInput.value = val + 1;
    });

// ####################### Cart Quantity Buttons #######################

function updateCart(productId, quantity) {
    fetch('@Url.Action("UpdateCartQuantity", "Products")', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': getAntiForgeryToken()
        },
        body: JSON.stringify({ productId: productId, quantity: parseInt(quantity) })
    })
        .then(res => res.json())
        .then(data => {
            if (data.success) {
                document.getElementById('cartTotal').innerText = data.total.toFixed(2);
            }
        });
}

document.querySelectorAll('.btn-increase').forEach(btn => {
    btn.addEventListener('click', () => {
        const productId = parseInt(btn.dataset.productid);
        const input = document.querySelector('.quantity-input[data-productid="' + productId + '"]');
        let val = parseInt(input.value);
        const max = parseInt(input.max);
        if (val < max) {
            input.value = val + 1;
            updateCart(productId, input.value);
        }
    });
});

document.querySelectorAll('.btn-decrease').forEach(btn => {
    btn.addEventListener('click', () => {
        const productId = parseInt(btn.dataset.productid);
        const input = document.querySelector('.quantity-input[data-productid="' + productId + '"]');
        let val = parseInt(input.value);
        if (val > 1) {
            input.value = val - 1;
            updateCart(productId, input.value);
        }
    });
});

document.querySelectorAll('.quantity-input').forEach(input => {
    input.addEventListener('change', () => {
        const productId = parseInt(input.dataset.productid);
        let val = parseInt(input.value);
        const max = parseInt(input.max);
        if (val < 1) val = 1;
        if (val > max) val = max;
        input.value = val;
        updateCart(productId, val);
    });
});

// ####################### Login-Signup Page #######################

$(document).ready(function () {
    var formToShow = "@ViewData["Form"]";
    if (formToShow === "signup") {
        $('#signupBtn').click();
    }
});

$(document).ready(function () {
    $('#loginForm').submit(function () {
        $('[data-valmsg-for="Role"]').hide();
    });
});