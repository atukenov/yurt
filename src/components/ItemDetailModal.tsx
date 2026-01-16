import { IMenuItem, ITopping } from "@/types";
import { QuantitySelector } from "./QuantitySelector";

interface ItemDetailModalProps {
  selectedItem: IMenuItem | null;
  onClose: () => void;
  selectedSize: "small" | "medium" | "large";
  setSelectedSize: (size: "small" | "medium" | "large") => void;
  selectedToppings: string[];
  setSelectedToppings: (toppings: string[]) => void;
  specialInstructions: string;
  setSpecialInstructions: (instructions: string) => void;
  quantity: number;
  setQuantity: (quantity: number) => void;
  toppings: ITopping[];
  calculatePrice: () => number;
  handleAddToCart: () => void;
  t: any;
}

export function ItemDetailModal({
  selectedItem,
  onClose,
  selectedSize,
  setSelectedSize,
  selectedToppings,
  setSelectedToppings,
  specialInstructions,
  setSpecialInstructions,
  quantity,
  setQuantity,
  toppings,
  calculatePrice,
  handleAddToCart,
  t,
}: ItemDetailModalProps) {
  const handleClose = () => {
    // Reset all selections
    setSelectedSize("medium");
    setSelectedToppings([]);
    setSpecialInstructions("");
    setQuantity(1);
    onClose();
  };

  if (!selectedItem) return null;

  return (
    <div className="fixed inset-0 bg-[#ffd1195d] bg-opacity-60 flex items-center justify-center p-4 z-50">
      <div className="bg-white rounded-lg max-w-md w-full max-h-screen overflow-y-auto">
        <div className="p-6">
          <div className="flex justify-between items-start mb-6">
            <h2 className="text-xl sm:text-2xl font-bold text-gray-900">
              {selectedItem.name}
            </h2>
            <button
              onClick={handleClose}
              className="text-gray-500 hover:text-gray-700 text-xl sm:text-2xl"
            >
              ×
            </button>
          </div>

          <p className="text-gray-600 mb-6">{selectedItem.description}</p>

          {/* Size Selection */}
          <div className="mb-6">
            <h3 className="font-semibold text-gray-900 mb-3">{t.selectSize}</h3>
            <div className="space-y-2">
              {selectedItem.sizes?.map((s) => (
                <label key={s.size} className="flex items-center">
                  <input
                    type="radio"
                    name="size"
                    value={s.size}
                    checked={selectedSize === s.size}
                    onChange={(e) =>
                      setSelectedSize(
                        e.target.value as "small" | "medium" | "large",
                      )
                    }
                    className="w-4 h-4"
                  />
                  <span className="ml-3 capitalize text-gray-700">
                    {s.size} (+{" "}
                    <span className="font-bold">
                      {s.priceModifier.toFixed(2)} ₸
                    </span>
                    )
                  </span>
                </label>
              ))}
            </div>
          </div>

          {/* Toppings Selection */}
          {toppings.length > 0 && (
            <div className="mb-6">
              <h3 className="font-semibold text-gray-900 mb-3">
                {t.addToppings}
              </h3>
              <div className="space-y-4">
                {Array.from(
                  new Set(toppings.map((t) => t.category || "other")),
                ).map((category) => (
                  <div key={category}>
                    <h4 className="text-sm font-bold! text-gray-700 mb-2 capitalize">
                      {t.toppingCategories?.[
                        category as keyof typeof t.toppingCategories
                      ] || category}
                    </h4>
                    <div className="space-y-2 pl-2">
                      {toppings
                        .filter((t) => (t.category || "Other") === category)
                        .map((topping) => (
                          <label
                            key={topping._id}
                            className="flex items-center"
                          >
                            <input
                              type="checkbox"
                              checked={selectedToppings.includes(topping._id)}
                              onChange={(e) => {
                                if (e.target.checked) {
                                  setSelectedToppings([
                                    ...selectedToppings,
                                    topping._id,
                                  ]);
                                } else {
                                  setSelectedToppings(
                                    selectedToppings.filter(
                                      (id) => id !== topping._id,
                                    ),
                                  );
                                }
                              }}
                              className="w-4 h-4"
                            />
                            <span className="ml-3 text-gray-700 text-sm">
                              {topping.name} (+
                              <span className="font-bold">
                                {topping.price.toFixed(2)} ₸
                              </span>
                              )
                            </span>
                          </label>
                        ))}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Special Instructions */}
          <div className="mb-6">
            <label className="block text-sm font-medium text-gray-700 mb-2">
              {t.specialInstructions}
            </label>
            <textarea
              value={specialInstructions}
              onChange={(e) => setSpecialInstructions(e.target.value)}
              placeholder="e.g., Extra hot, light foam..."
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:border-amber-500 focus:outline-none"
              rows={3}
            />
          </div>

          {/* Quantity, Price and Add to Cart */}
          <div className="border-t pt-6">
            <div className="flex items-center gap-4">
              {/* Quantity Selector */}
              <QuantitySelector
                quantity={quantity}
                onQuantityChange={setQuantity}
                onDelete={handleClose}
              />

              {/* Add to Cart Button with Price */}
              <button
                onClick={handleAddToCart}
                className="flex-1 px-4 py-3 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 transition font-semibold flex items-center justify-between"
              >
                <span>{t.addToCart}</span>
                <span className="text-lg font-bold">
                  {(calculatePrice() * quantity).toFixed(2)} ₸
                </span>
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
