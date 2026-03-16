import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { createChargingStationSchema } from "@/schemas/createChargingStationSchema";
import { instance } from "@/lib/httpRequest";
import { QUERY_KEYS } from "@/cache/queryKeys";
import { useFieldArray, useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router-dom";

// const STATIONS_API_URL = "http://localhost:5162/api/stations";

const dayOptions = [
  { value: 0, label: "Chủ nhật" },
  { value: 1, label: "Thứ 2" },
  { value: 2, label: "Thứ 3" },
  { value: 3, label: "Thứ 4" },
  { value: 4, label: "Thứ 5" },
  { value: 5, label: "Thứ 6" },
  { value: 6, label: "Thứ 7" },
];

const defaultOperatingHours = dayOptions.map((day) => ({
  dayOfWeek: day.value,
  isClosed: false,
  openTime: "06:00:00",
  closeTime: "23:00:00",
}));

const defaultSlot = {
  slotName: "",
  connectorType: "CCS2",
  powerKw: 50,
  basePricePerHour: 45000,
  positionX: 1,
  positionY: 1,
};

const createChargingStation = async (payload) => {
  const response = await instance.post("/stations", payload);
  return response.data;
};

const getErrorMessage = (error) => {
  const data = error?.response?.data;

  if (error?.message === "process is not defined") {
    return "Cấu hình frontend đang sai biến môi trường khi gọi API.";
  }

  if (typeof data === "string") {
    return data;
  }

  if (data?.message) {
    return data.message;
  }

  if (data?.error) {
    return data.error;
  }

  if (data?.title) {
    return data.title;
  }

  if (data?.errors) {
    const firstEntry = Object.values(data.errors)[0];
    if (Array.isArray(firstEntry) && firstEntry.length > 0) {
      return firstEntry[0];
    }
  }

  if (error?.message) {
    return error.message;
  }

  return "Tạo trạm sạc thất bại. Vui lòng kiểm tra lại dữ liệu.";
};

function FieldError({ message }) {
  if (!message) {
    return null;
  }

  return <p className="mt-1 text-sm text-red-600">{message}</p>;
}

export default function CreateChargingStation() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const {
    control,
    handleSubmit,
    register,
    reset,
    setError,
    watch,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(createChargingStationSchema),
    defaultValues: {
      name: "",
      address: "",
      description: "",
      latitude: 10.7295,
      longitude: 106.7218,
      layoutImageUrl: "",
      layoutWidth: 10,
      layoutHeight: 8,
      operatingHours: defaultOperatingHours,
      slots: [defaultSlot],
    },
  });

  const { fields, append, remove } = useFieldArray({
    control,
    name: "slots",
  });

  const operatingHours = watch("operatingHours");

  const createStationMutation = useMutation({
    mutationFn: createChargingStation,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.ownerStations });
      alert("Tạo trạm sạc thành công.");
      reset({
        name: "",
        address: "",
        description: "",
        latitude: 10.7295,
        longitude: 106.7218,
        layoutImageUrl: "",
        layoutWidth: 10,
        layoutHeight: 8,
        operatingHours: defaultOperatingHours,
        slots: [defaultSlot],
      });
      navigate("/stations");
    },
    onError: (error) => {
      const message = getErrorMessage(error);
      setError("root.serverError", {
        type: "server",
        message,
      });
    },
  });

  const onSubmit = (data) => {
    const payload = {
      ...data,
      description: data.description?.trim() || null,
      layoutImageUrl: data.layoutImageUrl?.trim() || null,
      latitude: data.latitude ?? null,
      longitude: data.longitude ?? null,
      operatingHours: data.operatingHours.map((item) => ({
        dayOfWeek: Number(item.dayOfWeek),
        isClosed: item.isClosed,
        openTime: item.isClosed ? null : item.openTime,
        closeTime: item.isClosed ? null : item.closeTime,
      })),
      slots: data.slots.map((slot) => ({
        ...slot,
        powerKw: Number(slot.powerKw),
        basePricePerHour: Number(slot.basePricePerHour),
        positionX: Number(slot.positionX),
        positionY: Number(slot.positionY),
      })),
    };

    createStationMutation.mutate(payload);
  };

  return (
    <div className="min-h-screen bg-slate-100 px-6 py-8 text-slate-900">
      <div className="mx-auto max-w-6xl">
        <div className="mb-6 flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-white px-6 py-5 shadow-sm ring-1 ring-slate-200">
          <div>
            <p className="text-sm font-medium uppercase tracking-[0.18em] text-orange-500">
              Owner Dashboard
            </p>
            <h1 className="mt-2 text-3xl font-bold">Tạo trạm sạc mới</h1>
            <p className="mt-2 text-sm text-slate-600">
              Khai báo thông tin trạm, sơ đồ mặt bằng và danh sách trụ sạc để
              gửi lên hệ thống.
            </p>
          </div>
          <Link
            to="/stations"
            className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:border-slate-400 hover:bg-slate-50"
          >
            Quay lại danh sách
          </Link>
        </div>

        <form className="space-y-6" onSubmit={handleSubmit(onSubmit)}>
          <div className="grid gap-6 lg:grid-cols-[1.2fr_0.8fr]">
            <section className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
              <h2 className="text-xl font-semibold">Thông tin cơ bản</h2>
              <div className="mt-5 grid gap-5 md:grid-cols-2">
                <div className="md:col-span-2">
                  <label className="mb-2 block text-sm font-medium text-slate-700">
                    Tên trạm
                  </label>
                  <input
                    {...register("name")}
                    className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400"
                    placeholder="Ví dụ: Trạm Sạc EV Quận 7"
                  />
                  <FieldError message={errors.name?.message} />
                </div>

                <div className="md:col-span-2">
                  <label className="mb-2 block text-sm font-medium text-slate-700">
                    Địa chỉ
                  </label>
                  <input
                    {...register("address")}
                    className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400"
                    placeholder="123 Nguyễn Hữu Thọ, Quận 7, TP.HCM"
                  />
                  <FieldError message={errors.address?.message} />
                </div>

                <div className="md:col-span-2">
                  <label className="mb-2 block text-sm font-medium text-slate-700">
                    Mô tả
                  </label>
                  <textarea
                    {...register("description")}
                    rows={4}
                    className="w-full rounded-xl border border-slate-300 px-4 py-3 outline-none transition focus:border-orange-400"
                    placeholder="Mô tả vị trí, tiện ích, thời gian hoạt động nổi bật..."
                  />
                  <FieldError message={errors.description?.message} />
                </div>

                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-700">
                    Vĩ độ
                  </label>
                  <input
                    type="number"
                    step="any"
                    {...register("latitude")}
                    className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400"
                    placeholder="10.7295"
                  />
                  <FieldError message={errors.latitude?.message} />
                </div>

                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-700">
                    Kinh độ
                  </label>
                  <input
                    type="number"
                    step="any"
                    {...register("longitude")}
                    className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400"
                    placeholder="106.7218"
                  />
                  <FieldError message={errors.longitude?.message} />
                </div>
              </div>
            </section>

            <section className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
              <h2 className="text-xl font-semibold">Mặt bằng trạm</h2>
              <div className="mt-5 space-y-5">
                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-700">
                    URL ảnh sơ đồ
                  </label>
                  <input
                    {...register("layoutImageUrl")}
                    className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400"
                    placeholder="https://..."
                  />
                  <FieldError message={errors.layoutImageUrl?.message} />
                </div>

                <div className="grid gap-5 md:grid-cols-2">
                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-700">
                      Chiều rộng layout
                    </label>
                    <input
                      type="number"
                      {...register("layoutWidth")}
                      className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400"
                    />
                    <FieldError message={errors.layoutWidth?.message} />
                  </div>

                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-700">
                      Chiều cao layout
                    </label>
                    <input
                      type="number"
                      {...register("layoutHeight")}
                      className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400"
                    />
                    <FieldError message={errors.layoutHeight?.message} />
                  </div>
                </div>

                <div className="rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-4 text-sm text-slate-600">
                  Hệ thống backend đang nhận tọa độ trụ sạc theo `positionX` và
                  `positionY` trên layout. Bạn có thể dùng kích thước layout này
                  để quy ước vị trí các trụ khi cấu hình.
                </div>
              </div>
            </section>
          </div>

          <section className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 className="text-xl font-semibold">Giờ hoạt động</h2>
                <p className="mt-1 text-sm text-slate-600">
                  Cấu hình cho từng ngày trong tuần. Nếu nghỉ, bật trạng thái
                  đóng cửa.
                </p>
              </div>
            </div>

            <div className="mt-5 space-y-4">
              {dayOptions.map((day, index) => {
                const dayErrors = errors.operatingHours?.[index];
                const isClosed = operatingHours?.[index]?.isClosed;

                return (
                  <div
                    key={day.value}
                    className="grid gap-4 rounded-2xl border border-slate-200 p-4 md:grid-cols-[1.2fr_1fr_1fr_auto] md:items-center"
                  >
                    <div>
                      <p className="font-medium text-slate-800">{day.label}</p>
                      <label className="mt-2 inline-flex items-center gap-2 text-sm text-slate-600">
                        <input
                          type="checkbox"
                          {...register(`operatingHours.${index}.isClosed`)}
                        />
                        Đóng cửa cả ngày
                      </label>
                    </div>

                    <div>
                      <label className="mb-2 block text-sm font-medium text-slate-700">
                        Mở cửa
                      </label>
                      <input
                        type="time"
                        step="1"
                        disabled={isClosed}
                        {...register(`operatingHours.${index}.openTime`)}
                        className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition disabled:cursor-not-allowed disabled:bg-slate-100 focus:border-orange-400"
                      />
                      <FieldError message={dayErrors?.openTime?.message} />
                    </div>

                    <div>
                      <label className="mb-2 block text-sm font-medium text-slate-700">
                        Đóng cửa
                      </label>
                      <input
                        type="time"
                        step="1"
                        disabled={isClosed}
                        {...register(`operatingHours.${index}.closeTime`)}
                        className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition disabled:cursor-not-allowed disabled:bg-slate-100 focus:border-orange-400"
                      />
                      <FieldError message={dayErrors?.closeTime?.message} />
                    </div>

                    <input
                      type="hidden"
                      {...register(`operatingHours.${index}.dayOfWeek`)}
                    />
                  </div>
                );
              })}
            </div>
          </section>

          <section className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 className="text-xl font-semibold">Danh sách trụ sạc</h2>
                <p className="mt-1 text-sm text-slate-600">
                  Thêm từng trụ, loại đầu sạc, công suất và vị trí trên layout.
                </p>
              </div>

              <Button
                type="button"
                className="bg-slate-900 text-white hover:bg-slate-800"
                onClick={() => append({ ...defaultSlot })}
              >
                Thêm trụ sạc
              </Button>
            </div>

            <FieldError message={errors.slots?.message} />

            <div className="mt-5 space-y-5">
              {fields.map((field, index) => {
                const slotErrors = errors.slots?.[index];

                return (
                  <div
                    key={field.id}
                    className="rounded-2xl border border-slate-200 p-5"
                  >
                    <div className="mb-4 flex items-center justify-between gap-3">
                      <h3 className="text-lg font-semibold text-slate-800">
                        Trụ sạc {index + 1}
                      </h3>
                      <button
                        type="button"
                        onClick={() => remove(index)}
                        disabled={fields.length === 1}
                        className="text-sm font-medium text-red-600 transition hover:text-red-700 disabled:cursor-not-allowed disabled:text-slate-300"
                      >
                        Xóa
                      </button>
                    </div>

                    <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
                      <div>
                        <label className="mb-2 block text-sm font-medium text-slate-700">
                          Tên trụ sạc
                        </label>
                        <input
                          {...register(`slots.${index}.slotName`)}
                          className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400"
                          placeholder="Ví dụ: Trụ B1"
                        />
                        <FieldError message={slotErrors?.slotName?.message} />
                      </div>

                      <div>
                        <label className="mb-2 block text-sm font-medium text-slate-700">
                          Loại đầu sạc
                        </label>
                        <select
                          {...register(`slots.${index}.connectorType`)}
                          className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400"
                        >
                          <option value="CCS2">CCS2</option>
                          <option value="CHAdeMO">CHAdeMO</option>
                          <option value="Type2">Type 2</option>
                          <option value="GB/T">GB/T</option>
                        </select>
                        <FieldError
                          message={slotErrors?.connectorType?.message}
                        />
                      </div>

                      <div>
                        <label className="mb-2 block text-sm font-medium text-slate-700">
                          Công suất (kW)
                        </label>
                        <input
                          type="number"
                          step="any"
                          {...register(`slots.${index}.powerKw`)}
                          className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400"
                        />
                        <FieldError message={slotErrors?.powerKw?.message} />
                      </div>

                      <div>
                        <label className="mb-2 block text-sm font-medium text-slate-700">
                          Giá theo giờ (VND)
                        </label>
                        <input
                          type="number"
                          {...register(`slots.${index}.basePricePerHour`)}
                          className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400"
                        />
                        <FieldError
                          message={slotErrors?.basePricePerHour?.message}
                        />
                      </div>

                      <div>
                        <label className="mb-2 block text-sm font-medium text-slate-700">
                          Vị trí X
                        </label>
                        <input
                          type="number"
                          step="any"
                          {...register(`slots.${index}.positionX`)}
                          className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400"
                        />
                        <FieldError message={slotErrors?.positionX?.message} />
                      </div>

                      <div>
                        <label className="mb-2 block text-sm font-medium text-slate-700">
                          Vị trí Y
                        </label>
                        <input
                          type="number"
                          step="any"
                          {...register(`slots.${index}.positionY`)}
                          className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400"
                        />
                        <FieldError message={slotErrors?.positionY?.message} />
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </section>

          <div className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
            {errors.root?.serverError?.message && (
              <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                {errors.root.serverError.message}
              </div>
            )}

            <div className="flex flex-wrap items-center justify-end gap-3">
              <Link
                to="/stations"
                className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:border-slate-400 hover:bg-slate-50"
              >
                Hủy
              </Link>
              <Button
                type="submit"
                className="h-11 bg-orange-500 px-6 text-white hover:bg-orange-600"
                disabled={createStationMutation.isPending}
              >
                {createStationMutation.isPending
                  ? "Đang tạo trạm..."
                  : "Tạo trạm sạc"}
              </Button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
