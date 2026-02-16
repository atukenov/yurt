import { MdDelete } from "react-icons/md";

interface QuantitySelectorProps {
  quantity: number;
  onQuantityChange: (quantity: number) => void;
  onDelete: () => void;
}

export function QuantitySelector({
  quantity,
  onQuantityChange,
  onDelete,
}: QuantitySelectorProps) {
  return (
    <div className="flex items-center gap-2 border border-gray-300 rounded-lg">
      <button
        onClick={() =>
          quantity === 1 ? onDelete() : onQuantityChange(quantity - 1)
        }
        className="px-3 py-2 hover:bg-gray-100 transition text-gray-700"
        title={quantity === 1 ? "Remove item" : "Decrease quantity"}
      >
        {quantity === 1 ? <MdDelete className="text-red-500 text-lg" /> : "−"}
      </button>
      <span className="text-lg font-semibold w-8 text-center">{quantity}</span>
      <button
        onClick={() => onQuantityChange(quantity + 1)}
        className="px-3 py-2 hover:bg-gray-100 transition"
      >
        +
      </button>
    </div>
  );
}
