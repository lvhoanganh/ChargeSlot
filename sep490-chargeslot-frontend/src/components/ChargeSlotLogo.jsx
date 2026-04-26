/**
 * ChargeSlotLogo — Premium SVG logo component for ChargeSlot.
 * Usage:
 *   <ChargeSlotLogo />                   // Icon only (navbar)
 *   <ChargeSlotLogo showText />          // Icon + "ChargeSlot" text
 *   <ChargeSlotLogo showText suffix="Admin" />  // Icon + "ChargeSlot" + badge
 *   <ChargeSlotLogo size={40} />         // Custom size
 */
export default function ChargeSlotLogo({
  size = 34,
  showText = false,
  suffix = "",
  className = "",
  dark = false,
}) {
  const textColor = dark
    ? "cs-logo__text--dark"
    : "cs-logo__text--light";

  return (
    <>
      <span className={`cs-logo ${className}`}>
        {/* SVG Icon */}
        <svg
          width={size}
          height={size}
          viewBox="0 0 120 120"
          fill="none"
          xmlns="http://www.w3.org/2000/svg"
          className="cs-logo__icon"
        >
          <defs>
            <linearGradient id="cs-grad-bg" x1="0" y1="0" x2="120" y2="120" gradientUnits="userSpaceOnUse">
              <stop offset="0%" stopColor="#f97316" />
              <stop offset="100%" stopColor="#ea580c" />
            </linearGradient>
            <linearGradient id="cs-grad-bolt" x1="50" y1="15" x2="70" y2="105" gradientUnits="userSpaceOnUse">
              <stop offset="0%" stopColor="#ffffff" />
              <stop offset="100%" stopColor="#fff7ed" />
            </linearGradient>
            <filter id="cs-shadow" x="-4" y="-4" width="128" height="128">
              <feDropShadow dx="0" dy="2" stdDeviation="4" floodColor="#ea580c" floodOpacity="0.3" />
            </filter>
          </defs>
          {/* Rounded square background */}
          <rect
            x="4" y="4" width="112" height="112" rx="28"
            fill="url(#cs-grad-bg)"
            filter="url(#cs-shadow)"
          />
          {/* Inner glow ring */}
          <rect
            x="12" y="12" width="96" height="96" rx="22"
            fill="none"
            stroke="rgba(255,255,255,0.2)"
            strokeWidth="1.5"
          />
          {/* Lightning bolt */}
          <path
            d="M66.5 18L42 62h18L48 102l36-50H64l14-34H66.5z"
            fill="url(#cs-grad-bolt)"
          />
          {/* Small slot lines at bottom */}
          <rect x="40" y="106" width="16" height="4" rx="2" fill="rgba(255,255,255,0.4)" />
          <rect x="62" y="106" width="16" height="4" rx="2" fill="rgba(255,255,255,0.4)" />
        </svg>

        {/* Text branding */}
        {showText && (
          <span className={`cs-logo__text ${textColor}`}>
            ChargeSlot
            {suffix && (
              <span className="cs-logo__suffix">{suffix}</span>
            )}
          </span>
        )}
      </span>

      <style>{`
        .cs-logo {
          display: inline-flex;
          align-items: center;
          gap: 10px;
          text-decoration: none;
          flex-shrink: 0;
        }
        .cs-logo__icon {
          transition: transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1);
          flex-shrink: 0;
        }
        .cs-logo:hover .cs-logo__icon {
          transform: scale(1.08) rotate(-3deg);
        }
        .cs-logo__text {
          font-size: 19px;
          font-weight: 800;
          letter-spacing: -0.5px;
          line-height: 1;
          display: inline-flex;
          align-items: center;
          gap: 6px;
        }
        .cs-logo__text--light {
          background: linear-gradient(135deg, #f97316 0%, #ea580c 100%);
          -webkit-background-clip: text;
          -webkit-text-fill-color: transparent;
          background-clip: text;
        }
        .cs-logo__text--dark {
          background: linear-gradient(135deg, #fb923c 0%, #f97316 100%);
          -webkit-background-clip: text;
          -webkit-text-fill-color: transparent;
          background-clip: text;
        }
        .cs-logo__suffix {
          font-size: 11px;
          font-weight: 700;
          letter-spacing: 0.5px;
          text-transform: uppercase;
          padding: 2px 8px;
          border-radius: 6px;
          background: linear-gradient(135deg, #f97316, #ea580c);
          color: white;
          -webkit-text-fill-color: white;
          background-clip: border-box;
          -webkit-background-clip: border-box;
        }
      `}</style>
    </>
  );
}
