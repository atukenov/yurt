import { useEffect, useState } from "react";
import { toast } from "react-toastify";
import { FaImage, FaTimes } from "react-icons/fa";

import Textfield from "#components/base/Textfield";
import { useAdmin } from "#components/context/useContext";
import { TMenu } from "#utils/database/models/menu";

import "./menuItemModal.scss";

const MenuItemModal = (props: TMenuItemModalProps) => {
  const { open, setOpen, editItem, onSave } = props;
  const { profile } = useAdmin();

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [category, setCategory] = useState("");
  const [price, setPrice] = useState("");
  const [taxPercent, setTaxPercent] = useState("");
  const [foodType, setFoodType] = useState<"spicy" | "extra-spicy" | "sweet" | "">("");
  const [veg, setVeg] = useState<"veg" | "non-veg" | "contains-egg">("veg");
  const [image, setImage] = useState("");
  const [hidden, setHidden] = useState(true);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (editItem) {
      setName(editItem.name || "");
      setDescription(editItem.description || "");
      setCategory(editItem.category || "");
      setPrice(editItem.price?.toString() || "");
      setTaxPercent(editItem.taxPercent?.toString() || "");
      setFoodType(editItem.foodType || "");
      setVeg(editItem.veg || "veg");
      setImage(editItem.image || "");
      setHidden(editItem.hidden ?? true);
    } else {
      resetForm();
    }
  }, [editItem, open]);

  const resetForm = () => {
    setName("");
    setDescription("");
    setCategory(profile?.categories?.[0] || "");
    setPrice("");
    setTaxPercent("");
    setFoodType("");
    setVeg("veg");
    setImage("");
    setHidden(true);
  };

  const handleSubmit = async () => {
    if (!name.trim()) return toast.error("Name is required");
    if (!category) return toast.error("Category is required");
    if (!price || isNaN(Number(price)) || Number(price) <= 0) return toast.error("Valid price is required");
    if (!taxPercent || isNaN(Number(taxPercent)) || Number(taxPercent) < 0) return toast.error("Valid tax percent is required");

    setLoading(true);

    const menuData = {
      name: name.trim(),
      description: description.trim(),
      category,
      price: Number(price),
      taxPercent: Number(taxPercent),
      foodType: foodType || undefined,
      veg,
      image: image.trim(),
      hidden,
    };

    try {
      const url = editItem ? `/api/admin/menu/${editItem._id}` : "/api/admin/menu";
      const method = editItem ? "PUT" : "POST";

      const req = await fetch(url, {
        method,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(menuData),
      });

      const res = await req.json();

      if (!req.ok) {
        toast.error(res?.message || "Failed to save menu item");
      } else {
        toast.success(editItem ? "Menu item updated successfully" : "Menu item created successfully");
        resetForm();
        setOpen(false);
        onSave();
      }
    } catch (error) {
      toast.error("An error occurred while saving");
    } finally {
      setLoading(false);
    }
  };

  if (!open) return null;

  return (
    <div className="menuItemModal">
      <div className="backdrop" onClick={() => setOpen(false)} />
      <div className="modalContent">
        <div className="modalHeader">
          <h2>{editItem ? "Edit Menu Item" : "Create New Menu Item"}</h2>
          <button className="closeButton xButton" onClick={() => setOpen(false)}>
            <FaTimes />
          </button>
        </div>

        <div className="modalBody">
          <div className="formSection">
            <label className="formLabel">Name *</label>
            <Textfield
              placeholder="Enter menu item name"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </div>

          <div className="formSection">
            <label className="formLabel">Description</label>
            <textarea
              className="textareaField"
              placeholder="Enter description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
            />
          </div>

          <div className="formRow">
            <div className="formSection">
              <label className="formLabel">Category *</label>
              <select
                className="selectField"
                value={category}
                onChange={(e) => setCategory(e.target.value)}
              >
                <option value="">Select category</option>
                {profile?.categories?.map((cat, i) => (
                  <option key={i} value={cat}>
                    {cat}
                  </option>
                ))}
              </select>
            </div>

            <div className="formSection">
              <label className="formLabel">Veg Type *</label>
              <select
                className="selectField"
                value={veg}
                onChange={(e) => setVeg(e.target.value as any)}
              >
                <option value="veg">Veg</option>
                <option value="non-veg">Non-Veg</option>
                <option value="contains-egg">Contains Egg</option>
              </select>
            </div>
          </div>

          <div className="formRow">
            <div className="formSection">
              <label className="formLabel">Price (₸) *</label>
              <Textfield
                type="number"
                placeholder="Enter price"
                value={price}
                onChange={(e) => setPrice(e.target.value)}
              />
            </div>

            <div className="formSection">
              <label className="formLabel">Tax (%) *</label>
              <Textfield
                type="number"
                placeholder="Enter tax %"
                value={taxPercent}
                onChange={(e) => setTaxPercent(e.target.value)}
              />
            </div>
          </div>

          <div className="formSection">
            <label className="formLabel">Food Type</label>
            <select
              className="selectField"
              value={foodType}
              onChange={(e) => setFoodType(e.target.value as any)}
            >
              <option value="">Select food type</option>
              <option value="spicy">Spicy</option>
              <option value="extra-spicy">Extra Spicy</option>
              <option value="sweet">Sweet</option>
            </select>
          </div>

          <div className="formSection">
            <label className="formLabel">Image URL</label>
            <Textfield
              placeholder="Enter image URL"
              value={image}
              onChange={(e) => setImage(e.target.value)}
            />
            {image && (
              <div className="imagePreview">
                <img src={image} alt="Preview" onError={(e) => (e.currentTarget.style.display = "none")} />
              </div>
            )}
          </div>

          <div className="formSection checkboxSection">
            <label className="checkboxLabel">
              <input
                type="checkbox"
                checked={hidden}
                onChange={(e) => setHidden(e.target.checked)}
              />
              <span>Hidden (not visible to customers)</span>
            </label>
          </div>
        </div>

        <div className="modalFooter">
          <button
            className="xButton secondary"
            onClick={() => setOpen(false)}
            disabled={loading}
          >
            Cancel
          </button>
          <button
            className="xButton primary"
            onClick={handleSubmit}
            disabled={loading}
          >
            {loading ? "Saving..." : editItem ? "Update" : "Create"}
          </button>
        </div>
      </div>
    </div>
  );
};

export default MenuItemModal;

type TMenuItemModalProps = {
  open: boolean;
  setOpen: (open: boolean) => void;
  editItem?: TMenu;
  onSave: () => void;
};
