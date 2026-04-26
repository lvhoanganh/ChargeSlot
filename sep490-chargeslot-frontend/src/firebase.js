import { initializeApp } from "firebase/app";
import { getAuth } from "firebase/auth";

const firebaseConfig = {
  apiKey: "AIzaSyA9QEDESU15E4iYdceGPR1GyzO3P75eSWE",
  authDomain: "chargeslot-42b86.firebaseapp.com",
  projectId: "chargeslot-42b86",
  storageBucket: "chargeslot-42b86.firebasestorage.app",
  messagingSenderId: "120252239809",
  appId: "1:120252239809:web:d4bc75ff81d8ac58426940",
};

const app = initializeApp(firebaseConfig);
export const auth = getAuth(app);
auth.languageCode = 'vi'; // SMS OTP gửi bằng tiếng Việt
