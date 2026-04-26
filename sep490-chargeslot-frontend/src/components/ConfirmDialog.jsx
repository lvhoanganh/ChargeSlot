import { useState, useEffect } from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/button";

let resolveCallback = null;
const listeners = new Set();

/** Show a promise-based confirm dialog.
 *  @param {string} message
 *  @param {string} title
 *  @returns {Promise<boolean>}
 */
export function showConfirm(message, title = "Xác nhận") {
  return new Promise((resolve) => {
    listeners.forEach((fn) => fn({ message, title, resolve }));
  });
}

export function ConfirmDialogContainer() {
  const [dialog, setDialog] = useState(null);

  useEffect(() => {
    const handler = (d) => setDialog(d);
    listeners.add(handler);
    return () => listeners.delete(handler);
  }, []);

  if (!dialog) return null;

  const handleClose = (res) => {
    if (dialog.resolve) dialog.resolve(res);
    setDialog(null);
  };

  return createPortal(
    <div className="fixed inset-0 z-[99999] flex items-center justify-center bg-slate-900/40 p-4 transition-all duration-200">
      <div 
        className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl"
        style={{ animation: "toast-slide-in .2s ease-out" }}
      >
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-full bg-orange-100">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#f97316" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
              <line x1="12" y1="9" x2="12" y2="13" />
              <line x1="12" y1="17" x2="12.01" y2="17" />
            </svg>
          </div>
          <h3 className="text-lg font-bold text-slate-900">{dialog.title}</h3>
        </div>
        <p className="mt-4 text-[15px] text-slate-600 leading-relaxed ml-13">
          {dialog.message}
        </p>
        <div className="mt-8 flex justify-end gap-3">
          <Button 
            variant="outline" 
            onClick={() => handleClose(false)} 
            className="rounded-xl border-slate-200 px-5 text-slate-600 hover:bg-slate-50 cursor-pointer"
          >
            Hủy
          </Button>
          <Button 
            onClick={() => handleClose(true)} 
            className="rounded-xl bg-orange-500 px-6 text-white hover:bg-orange-600 shadow-md shadow-orange-500/20 cursor-pointer"
          >
            Xác nhận
          </Button>
        </div>
      </div>
    </div>,
    document.body
  );
}
