import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { favoriteApi } from "@/services/api";

export default function FavoriteStations() {
  const navigate = useNavigate();
  const [favorites, setFavorites] = useState([]);
  const [loading, setLoading] = useState(true);
  const [imgErrors, setImgErrors] = useState({});

  useEffect(() => {
    setLoading(true);
    favoriteApi.getMyFavorites()
      .then(data => setFavorites(Array.isArray(data) ? data : []))
      .catch(() => setFavorites([]))
      .finally(() => setLoading(false));
  }, []);

  async function removeFav(stationId) {
    try {
      await favoriteApi.remove(stationId);
      setFavorites(prev => prev.filter(f => f.stationId !== stationId));
    } catch { /* ignore */ }
  }

  function getImg(f) {
    if (imgErrors[f.stationId] || !f.imageUrl) return null;
    return f.imageUrl.startsWith("http") ? f.imageUrl : `http://localhost:5162${f.imageUrl}`;
  }

  if (loading) {
    return (
      <div className="min-h-screen bg-[#f3f4f5] pt-24 px-4">
        <div className="max-w-3xl mx-auto text-center">
          <div className="text-5xl mb-4">❤️</div>
          <p className="text-gray-500">Đang tải danh sách yêu thích...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-[#f3f4f5] pt-24 px-4 pb-8">
      <div className="max-w-3xl mx-auto">
        {/* Header */}
        <div className="flex items-center gap-3 mb-6">
          <div className="w-11 h-11 rounded-xl flex items-center justify-center"
            style={{ background: "linear-gradient(135deg, #ef4444, #dc2626)" }}>
            <svg width="22" height="22" viewBox="0 0 24 24" fill="#fff" stroke="#fff" strokeWidth="0">
              <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
            </svg>
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Trạm yêu thích</h1>
            <p className="text-sm text-gray-500">{favorites.length} trạm</p>
          </div>
        </div>

        {favorites.length === 0 ? (
          <div className="text-center py-16">
            <div className="text-6xl mb-4 opacity-40">💔</div>
            <h2 className="text-lg font-bold text-gray-600 mb-2">Chưa có trạm yêu thích</h2>
            <p className="text-sm text-gray-400 mb-6">Thêm trạm yêu thích từ bản đồ hoặc trang chi tiết trạm</p>
            <button
              onClick={() => navigate("/driver/map")}
              className="px-6 py-3 bg-orange-500 hover:bg-orange-600 text-white font-semibold rounded-xl transition-colors cursor-pointer"
            >
              🗺️ Tìm trạm sạc
            </button>
          </div>
        ) : (
          <div className="space-y-4">
            {favorites.map(f => {
              const img = getImg(f);
              return (
                <div key={f.stationId} className="bg-white rounded-2xl shadow-sm overflow-hidden flex hover:shadow-md transition-shadow">
                  {/* Image */}
                  <div className="w-36 h-36 flex-shrink-0 relative">
                    {img ? (
                      <img
                        src={img} alt={f.name}
                        className="w-full h-full object-cover"
                        onError={() => setImgErrors(prev => ({ ...prev, [f.stationId]: true }))}
                      />
                    ) : (
                      <div className="w-full h-full flex items-center justify-center"
                        style={{ background: "linear-gradient(135deg, #fef2f2, #fee2e2)" }}>
                        <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="#ef4444" strokeWidth="2">
                          <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
                        </svg>
                      </div>
                    )}
                  </div>

                  {/* Info */}
                  <div className="flex-1 p-4 flex flex-col justify-between">
                    <div>
                      <div className="flex items-center justify-between mb-1">
                        <h3 className="font-bold text-gray-900 text-base">{f.name}</h3>
                        {f.averageRating > 0 && (
                          <span className="text-xs font-bold text-amber-500 flex items-center gap-1">
                            ⭐ {Number(f.averageRating).toFixed(1)}
                            <span className="text-gray-400 font-normal">({f.totalReviews})</span>
                          </span>
                        )}
                      </div>
                      <p className="text-xs text-gray-500 flex items-center gap-1 mb-2">
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                          <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                          <circle cx="12" cy="10" r="3" />
                        </svg>
                        {f.address}
                      </p>
                    </div>

                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => navigate(`/driver/station/${f.stationId}`)}
                        className="flex-1 py-2 text-sm font-semibold text-white rounded-lg cursor-pointer transition-colors"
                        style={{ background: "linear-gradient(135deg, #f97316, #ea580c)" }}
                      >
                        Xem chi tiết
                      </button>
                      <button
                        onClick={() => removeFav(f.stationId)}
                        className="p-2 rounded-lg border border-red-200 hover:bg-red-50 transition-colors cursor-pointer"
                        title="Bỏ yêu thích"
                      >
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="#ef4444" stroke="#ef4444" strokeWidth="2">
                          <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
                        </svg>
                      </button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
