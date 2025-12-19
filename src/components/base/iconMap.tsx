import {
	FaPlus,
	FaMinus,
	FaTimes,
	FaCheck,
	FaChevronLeft,
	FaChevronRight,
	FaChevronDown,
	FaChevronUp,
	FaShoppingCart,
	FaHome,
	FaUtensils,
	FaStar,
	FaPhone,
	FaSignOutAlt,
	FaEye,
	FaEyeSlash,
	FaPen,
	FaInfoCircle,
	FaFileDownload,
	FaLeaf,
	FaEgg,
	FaDrumstickBite,
	FaEnvelope,
	FaUserCircle,
	FaSpinner,
	FaUser,
	FaSearch,
} from 'react-icons/fa';
import { MdDashboard, MdRestaurantMenu } from 'react-icons/md';
import { ReactNode } from 'react';

export type IconCode = string;

// Centralized icon mapping for all legacy xtreme-ui icon codes
export const getIconComponents = () => ({
	// Math/Actions
	'2b': FaPlus,
	'2d': FaMinus,
	'f00c': FaCheck,
	'f00d': FaTimes,
	
	// Chevrons/Arrows
	'f053': FaChevronLeft,
	'f054': FaChevronRight,
	'f063': FaChevronDown,
	'f078': FaChevronUp,
	
	// Shopping/Cart
	'e43b': FaShoppingCart,
	'e1bc': FaShoppingCart,
	
	// Navigation
	'f015': FaHome,
	'e09f': MdDashboard,
	'e3e3': MdRestaurantMenu,
	
	// Info/Actions
	'f05a': FaInfoCircle,
	'f354': FaFileDownload,
	'f002': FaSearch,
	
	// Visibility
	'f06e': FaEye,
	'f070': FaEyeSlash,
	
	// Edit
	'f304': FaPen,
	
	// Food/Veg Icons
	'f4d8': FaLeaf,
	'f6d6': FaDrumstickBite,
	'f7fb': FaEgg,
	
	// Reviews/Contact
	'f4ad': FaStar,
	'f8d3': FaPhone,
	'f095': FaPhone,
	
	// Auth/User
	'f011': FaSignOutAlt,
	'f0e0': FaEnvelope,
	'e323': FaUserCircle,
	'f86b': FaUserCircle,
	'f007': FaUser,
	
	// Payment/Checkout
	'f09d': FaCheck,
	
	// Modal close
	'e59b': FaTimes,
	
	// Loading
	'f110': FaSpinner,
});

export const iconMap: Record<IconCode, ReactNode> = Object.entries(getIconComponents()).reduce(
	(acc, [code, Component]) => {
		acc[code as IconCode] = <Component />;
		return acc;
	},
	{} as Record<IconCode, ReactNode>
);
