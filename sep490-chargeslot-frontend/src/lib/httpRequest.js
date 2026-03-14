import axios from "axios";
export const instance = axios.create({
  baseURL: "http://localhost:5162/api",
});

instance.interceptors.request.use((config) => {
  const token = localStorage.getItem("accessToken");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// let refreshPromise = null;
// instance.interceptors.response.use(
//   (response) => {
//     return response;
//   },
//   async (error) => {
//     console.log(error.response);
//     if (error.response.status === 401) {
//     //   if (!refreshPromise) {
//     //     refreshPromise = useAuth.getState().refreshAccessToken();
//     //   }
//     //   try {
//     //     await refreshPromise;
//     //   } finally {
//     //     refreshPromise = null;
//     //   }
//       return instance(error.config);
//     }
//     return Promise.reject(error);
//   },
// );
