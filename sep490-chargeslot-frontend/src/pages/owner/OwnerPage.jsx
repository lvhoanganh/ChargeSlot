import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { instance } from "@/lib/httpRequest";
import { QUERY_KEYS } from "@/cache/queryKeys";

const APPROVAL_STYLE = {
  Draft: "bg-slate-200 text-slate-700",
  PendingApproval: "bg-amber-100 text-amber-700",
  Approved: "bg-emerald-100 text-emerald-700",
  Rejected: "bg-rose-100 text-rose-700",
};

const APPROVAL_LABEL = {
  Draft: "Bản nháp",
  PendingApproval: "Chờ duyệt",
  Approved: "Đã duyệt",
  Rejected: "Bị từ chối",
};

const OPERATIONAL_STYLE = {
  Active: "bg-green-100 text-green-700",
  Inactive: "bg-zinc-200 text-zinc-700",
};

const OPERATIONAL_LABEL = {
  Active: "Đang hoạt động",
  Inactive: "Tạm ngưng",
};

const FILTER_DEFAULT = "Approved";

const FILTER_EMPTY_MESSAGE = {
  Approved: "Chưa có trạm sạc nào đang hoạt động.",
  PendingApproval: "Hiện chưa có trạm sạc nào đang chờ duyệt.",
  Rejected: "Hiện chưa có trạm sạc nào bị từ chối.",
};

const getErrorMessage = (error, fallback) => {
  const data = error?.response?.data;

  if (typeof data === "string") return data;
  if (data?.message) return data.message;
  if (data?.error) return data.error;
  if (data?.title) return data.title;

  if (data?.errors && typeof data.errors === "object") {
    const firstEntry = Object.values(data.errors)[0];
    if (Array.isArray(firstEntry) && firstEntry.length > 0) {
      return firstEntry[0];
    }
  }

  return fallback;
};

const getMyStations = async () => {
  const response = await instance.get("/stations");
  return response.data;
};

const deleteStation = async (stationId) => {
  await instance.delete(`/stations/${stationId}`);
};

const submitForApproval = async (stationId) => {
  await instance.post(`/stations/${stationId}/submit`);
};

function formatDateTime(value) {
  if (!value) return "-";

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return "-";

  return parsed.toLocaleString("vi-VN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export default function OwnerPage() {
  const queryClient = useQueryClient();
  const [selectedFilter, setSelectedFilter] = useState(FILTER_DEFAULT);

  const {
    data: stations = [],
    isLoading,
    isFetching,
    error,
    refetch,
  } = useQuery({
    queryKey: QUERY_KEYS.ownerStations,
    queryFn: getMyStations,
  });

  const deleteMutation = useMutation({
    mutationFn: deleteStation,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.ownerStations });
    },
  });

  const submitMutation = useMutation({
    mutationFn: submitForApproval,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.ownerStations });
    },
  });

  const filteredStations = useMemo(() => {
    const source = Array.isArray(stations) ? stations : [];
    return source.filter(
      (station) => station.approvalStatus === selectedFilter,
    );
  }, [stations, selectedFilter]);

  const queryErrorMessage = error
    ? getErrorMessage(error, "Không thể tải danh sách trạm sạc.")
    : "";

  const handleDelete = async (station) => {
    const accepted = window.confirm(
      `Bạn có chắc chắn muốn xóa trạm "${station.name}"?`,
    );
    if (!accepted) return;

    try {
      await deleteMutation.mutateAsync(station.id);
    } catch (err) {
      alert(getErrorMessage(err, "Không thể xóa trạm sạc."));
    }
  };

  const handleSubmitForApproval = async (station) => {
    const accepted = window.confirm(
      `Gửi trạm "${station.name}" lên hệ thống để duyệt?`,
    );
    if (!accepted) return;

    try {
      await submitMutation.mutateAsync(station.id);
    } catch (err) {
      alert(getErrorMessage(err, "Không thể gửi trạm để duyệt."));
    }
  };

  const handleFilterClick = (status) => {
    setSelectedFilter((prev) => (prev === status ? FILTER_DEFAULT : status));
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-slate-100 px-6 py-8">
        <div className="mx-auto max-w-7xl">
          <div className="rounded-2xl bg-white p-8 text-center text-slate-600 shadow-sm ring-1 ring-slate-200">
            Đang tải danh sách trạm sạc...
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-100 px-6 py-8 text-slate-900">
      <div className="mx-auto max-w-7xl space-y-6">
        <section className="flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-white px-6 py-5 shadow-sm ring-1 ring-slate-200">
          <div>
            <p className="text-sm font-medium uppercase tracking-[0.18em] text-orange-500">
              Owner Dashboard
            </p>
            <h1 className="mt-2 text-3xl font-bold">Danh sách trạm sạc</h1>
            <p className="mt-2 text-sm text-slate-600">
              Danh sách mặc định chỉ hiển thị các trạm đã được duyệt và đang
              hoạt động trên hệ thống.
            </p>
          </div>

          <div className="flex gap-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => refetch()}
              disabled={
                isFetching ||
                deleteMutation.isPending ||
                submitMutation.isPending
              }
            >
              {isFetching ? "Đang làm mới..." : "Làm mới"}
            </Button>
            <Button
              asChild
              className="bg-orange-500 text-white hover:bg-orange-600"
            >
              <Link to="/stations/add">Tạo trạm mới</Link>
            </Button>
          </div>
        </section>

        <section className="rounded-2xl bg-white p-4 shadow-sm ring-1 ring-slate-200">
          <div className="flex flex-wrap items-center gap-2">
            <p className="mr-2 text-sm font-medium text-slate-700">
              Bộ lọc nhanh:
            </p>
            <Button
              type="button"
              variant={selectedFilter === "Approved" ? "default" : "outline"}
              className={
                selectedFilter === "Approved"
                  ? "bg-emerald-600 text-white hover:bg-emerald-700"
                  : ""
              }
              onClick={() => setSelectedFilter("Approved")}
            >
              Trạm đang hoạt động
            </Button>
            <Button
              type="button"
              variant={
                selectedFilter === "PendingApproval" ? "default" : "outline"
              }
              className={
                selectedFilter === "PendingApproval"
                  ? "bg-amber-500 text-white hover:bg-amber-600"
                  : ""
              }
              onClick={() => handleFilterClick("PendingApproval")}
            >
              Trạm đang chờ duyệt
            </Button>
            <Button
              type="button"
              variant={selectedFilter === "Rejected" ? "default" : "outline"}
              className={
                selectedFilter === "Rejected"
                  ? "bg-rose-500 text-white hover:bg-rose-600"
                  : ""
              }
              onClick={() => handleFilterClick("Rejected")}
            >
              Trạm bị từ chối
            </Button>
          </div>
        </section>

        {queryErrorMessage && (
          <section className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">
            {queryErrorMessage}
          </section>
        )}

        {!filteredStations.length ? (
          <section className="rounded-2xl bg-white p-10 text-center shadow-sm ring-1 ring-slate-200">
            <h2 className="text-xl font-semibold text-slate-900">
              {FILTER_EMPTY_MESSAGE[selectedFilter]}
            </h2>
            <p className="mt-2 text-sm text-slate-600">
              Bạn có thể tạo trạm mới hoặc chuyển bộ lọc để kiểm tra các trạng
              thái khác.
            </p>
            <Button
              asChild
              className="mt-5 bg-orange-500 text-white hover:bg-orange-600"
            >
              <Link to="/stations/add">Tạo trạm sạc</Link>
            </Button>
          </section>
        ) : (
          <section className="space-y-4">
            {filteredStations.map((station) => {
              const canEditOrDelete =
                station.approvalStatus === "Draft" ||
                station.approvalStatus === "Rejected";

              const slotCount = station?.chargingSlots?.length || 0;
              const activeSlotCount =
                station?.chargingSlots?.filter((s) => s.status === "Active")
                  ?.length || 0;

              return (
                <article
                  key={station.id}
                  className="rounded-2xl bg-white p-5 shadow-sm ring-1 ring-slate-200"
                >
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="flex items-start gap-4">
                      {station?.images?.[0]?.imageUrl ? (
                        <img
                          src={station.images[0].imageUrl}
                          alt={`Ảnh trạm ${station.name}`}
                          className="h-24 w-36 rounded-lg object-cover ring-1 ring-slate-200"
                        />
                      ) : (
                        <div className="flex h-24 w-36 items-center justify-center rounded-lg bg-slate-100 text-xs text-slate-500 ring-1 ring-slate-200">
                          Chưa có ảnh
                        </div>
                      )}

                      <div className="space-y-1">
                        <h2 className="text-xl font-semibold text-slate-900">
                          {station.name}
                        </h2>
                        <p className="text-sm text-slate-600">
                          {station.address}
                        </p>
                        <p className="text-xs text-slate-500">
                          Tạo lúc: {formatDateTime(station.createdAt)}
                        </p>
                      </div>
                    </div>

                    <div className="flex flex-wrap items-center gap-2">
                      <span
                        className={`rounded-full px-3 py-1 text-xs font-semibold ${
                          APPROVAL_STYLE[station.approvalStatus] ||
                          "bg-slate-200 text-slate-700"
                        }`}
                      >
                        {APPROVAL_LABEL[station.approvalStatus] ||
                          station.approvalStatus}
                      </span>

                      <span
                        className={`rounded-full px-3 py-1 text-xs font-semibold ${
                          OPERATIONAL_STYLE[station.operationalStatus] ||
                          "bg-zinc-200 text-zinc-700"
                        }`}
                      >
                        {OPERATIONAL_LABEL[station.operationalStatus] ||
                          station.operationalStatus}
                      </span>
                    </div>
                  </div>

                  <div className="mt-4 grid gap-3 text-sm md:grid-cols-2 xl:grid-cols-3">
                    <div className="rounded-xl bg-slate-50 px-3 py-2">
                      <p className="text-xs uppercase tracking-wide text-slate-500">
                        Số trụ sạc
                      </p>
                      <p className="mt-1 font-semibold text-slate-800">
                        {slotCount}
                      </p>
                    </div>

                    <div className="rounded-xl bg-slate-50 px-3 py-2">
                      <p className="text-xs uppercase tracking-wide text-slate-500">
                        Trụ đang hoạt động
                      </p>
                      <p className="mt-1 font-semibold text-slate-800">
                        {activeSlotCount}
                      </p>
                    </div>

                    <div className="rounded-xl bg-slate-50 px-3 py-2">
                      <p className="text-xs uppercase tracking-wide text-slate-500">
                        Giờ hoạt động
                      </p>
                      <p className="mt-1 font-semibold text-slate-800">
                        {station?.operatingHours?.length || 0} ngày
                      </p>
                    </div>
                  </div>

                  {station.approvalStatus === "Rejected" &&
                    station.adminNote && (
                      <div className="mt-4 rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700">
                        <p className="font-semibold">Ghi chú từ admin</p>
                        <p className="mt-1">{station.adminNote}</p>
                      </div>
                    )}

                  <div className="mt-5 flex flex-wrap gap-2">
                    <Button asChild variant="outline">
                      <Link to={`/stations/${station.id}`}>Xem chi tiết</Link>
                    </Button>

                    {canEditOrDelete && (
                      <Button
                        type="button"
                        variant="destructive"
                        onClick={() => handleDelete(station)}
                        disabled={deleteMutation.isPending}
                      >
                        {deleteMutation.isPending ? "Đang xóa..." : "Xóa"}
                      </Button>
                    )}

                    {station.approvalStatus === "Rejected" && (
                      <Button
                        type="button"
                        className="bg-slate-900 text-white hover:bg-slate-800"
                        onClick={() => handleSubmitForApproval(station)}
                        disabled={submitMutation.isPending}
                      >
                        {submitMutation.isPending
                          ? "Đang gửi..."
                          : "Gửi phê duyệt"}
                      </Button>
                    )}

                    {station.approvalStatus === "PendingApproval" && (
                      <p className="self-center text-sm text-amber-700">
                        Trạm đang chờ admin duyệt.
                      </p>
                    )}

                    {station.approvalStatus === "Approved" && (
                      <p className="self-center text-sm text-emerald-700">
                        Trạm đã được phê duyệt và hiển thị trên hệ thống.
                      </p>
                    )}
                  </div>
                </article>
              );
            })}
          </section>
        )}
      </div>
    </div>
  );
}
