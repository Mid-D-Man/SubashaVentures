// Import the functions you need from the SDKs you need
import { initializeApp } from "firebase/app";
import { getAnalytics } from "firebase/analytics";
// TODO: Add SDKs for Firebase products that you want to use
// https://firebase.google.com/docs/web/setup#available-libraries

// Your web app's Firebase configuration
// For Firebase JS SDK v7.20.0 and later, measurementId is optional
const firebaseConfig = {
    apiKey: "AIzaSyB_CcjQaquB9Sj0MMogxIEVBK-rlFmHE9g",
    authDomain: "subashaventureswebstore.firebaseapp.com",
    projectId: "subashaventureswebstore",
    storageBucket: "subashaventureswebstore.firebasestorage.app",
    messagingSenderId: "1056961189823",
    appId: "1:1056961189823:web:1e1850118e58b91504b30a",
    measurementId: "G-60Y3CK40G8"
};

// Initialize Firebase
const app = initializeApp(firebaseConfig);
const analytics = getAnalytics(app);