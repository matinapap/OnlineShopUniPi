// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

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

function validateFields() {
    const firstName = document.getElementById('FirstName').value.trim();
    const lastName = document.getElementById('LastName').value.trim();
    const email = document.getElementById('Email').value.trim();
    const phoneNumber = document.getElementById('PhoneNumber').value.trim();
    const city = document.getElementById('citySelect').value;
    const address = document.getElementById('Address').value.trim();
    const password = document.getElementById('PasswordHash').value;
    const confirmPassword = document.getElementById('ConfirmPassword').value;

    const nameRegex = /^[A-Za-z\s]+$/;
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

    if (!emailRegex.test(email)) {
        alert("Παρακαλώ εισάγετε ένα έγκυρο email.");
        return false;
    }

    if (!phoneRegex.test(phoneNumber)) {
        alert("Το πεδίο 'Phone Number' πρέπει να περιέχει μόνο αριθμούς.");
        return false;
    }

    //if (!passwordRegex.test(password)) {
    //    alert("Ο κωδικός πρέπει να περιέχει τουλάχιστον 8 χαρακτήρες, έναν κεφαλαίο και έναν αριθμό.");
    //    return false;
    //}

    if (password !== confirmPassword) {
        alert("Οι κωδικοί δεν ταιριάζουν.");
        return false;
    }

    return true;
}