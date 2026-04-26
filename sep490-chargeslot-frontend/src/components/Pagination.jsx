export default function Pagination({ page, totalCount, pageSize = 20, onPageChange }) {
  const totalPages = Math.ceil(totalCount / pageSize);

  if (totalPages <= 1) return null;

  return (
    <div style={{
      display: "flex", gap: "8px", justifyContent: "center", alignItems: "center",
      marginTop: "24px", marginBottom: "24px", flexWrap: "wrap"
    }}>
      <button
        disabled={page <= 1}
        onClick={() => onPageChange(page - 1)}
        style={{
          padding: "8px 12px", border: "1px solid #e2e8f0", borderRadius: "8px",
          background: page <= 1 ? "#f8fafc" : "#fff", color: page <= 1 ? "#cbd5e1" : "#1e293b",
          cursor: page <= 1 ? "not-allowed" : "pointer", fontWeight: 600, fontSize: 13
        }}
      >
        Trước
      </button>

      {/* Basic numbered pagination */}
      {Array.from({ length: totalPages }).map((_, idx) => {
        const p = idx + 1;
        // Limit large numbers of pages
        if (totalPages > 5) {
          if (p !== 1 && p !== totalPages && Math.abs(p - page) > 1) {
            if (p === 2 || p === totalPages - 1) {
              return <span key={idx} style={{ color: "#94a3b8" }}>...</span>;
            }
            return null;
          }
        }
        
        return (
          <button
            key={p}
            onClick={() => onPageChange(p)}
            style={{
              width: "36px", height: "36px", border: "1px solid", borderRadius: "8px",
              display: "flex", justifyContent: "center", alignItems: "center",
              fontWeight: 600, fontSize: 14,
              borderColor: page === p ? "#f97316" : "#e2e8f0",
              background: page === p ? "#fff7ed" : "#fff",
              color: page === p ? "#ea580c" : "#64748b",
              cursor: page === p ? "default" : "pointer",
            }}
          >
            {p}
          </button>
        );
      })}

      <button
        disabled={page >= totalPages}
        onClick={() => onPageChange(page + 1)}
        style={{
          padding: "8px 12px", border: "1px solid #e2e8f0", borderRadius: "8px",
          background: page >= totalPages ? "#f8fafc" : "#fff", color: page >= totalPages ? "#cbd5e1" : "#1e293b",
          cursor: page >= totalPages ? "not-allowed" : "pointer", fontWeight: 600, fontSize: 13
        }}
      >
        Sau
      </button>
    </div>
  );
}
