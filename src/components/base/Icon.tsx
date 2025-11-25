import { ReactNode } from 'react';
import {
	FaPlus,
	FaMinus,
	FaTimes,
	FaCheck,
	FaChevronLeft,
	FaChevronRight,
	FaChevronDown,
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
	FaChevronUp,
	FaUser,
	FaSearch,
} from 'react-icons/fa';
import { MdDashboard, MdRestaurantMenu } from 'react-icons/md';

type IconCode = string;

// Map legacy xtreme-ui icon codes to react-icons
const iconMap: Record<IconCode, ReactNode> = {
	// Math/Actions
	'2b': <FaPlus />, // plus
	'2d': <FaMinus />, // minus
	'f00c': <FaCheck />, // check
	'f00d': <FaTimes />, // times/close
	
	// Chevrons/Arrows
	'f053': <FaChevronLeft />, // chevron-left
	'f054': <FaChevronRight />, // chevron-right
	'f063': <FaChevronDown />, // chevron-down
	'f078': <FaChevronUp />, // chevron-up
	
	// Shopping/Cart
	'e43b': <FaShoppingCart />, // shopping-cart
	'e1bc': <FaShoppingCart />, // shopping-cart variant
	
	// Navigation
	'f015': <FaHome />, // home
	'e09f': <MdDashboard />, // dashboard
	'e3e3': <MdRestaurantMenu />, // menu
	
	// Info/Actions
	'f05a': <FaInfoCircle />, // info-circle
	'f354': <FaFileDownload />, // file-download
	'f002': <FaSearch />, // search
	
	// Visibility
	'f06e': <FaEye />, // eye (visible)
	'f070': <FaEyeSlash />, // eye-slash (hidden)
	
	// Edit
	'f304': <FaPen />, // pen/edit
	
	// Food/Veg Icons
	'f4d8': <FaLeaf />, // leaf (veg)
	'f6d6': <FaDrumstickBite />, // drumstick (non-veg)
	'f7fb': <FaEgg />, // egg (contains-egg)
	
	// Reviews/Contact
	'f4ad': <FaStar />, // star (reviews)
	'f8d3': <FaPhone />, // phone (contact)
	'f095': <FaPhone />, // phone variant
	
	// Auth/User
	'f011': <FaSignOutAlt />, // sign-out
	'f0e0': <FaEnvelope />, // envelope/email
	'e323': <FaUserCircle />, // user-circle
	'f86b': <FaUserCircle />, // user-circle variant
	'f007': <FaUser />, // user
	
	// Payment/Checkout
	'f09d': <FaCheck />, // check-circle (proceed to pay)
	
	// Modal close
	'e59b': <FaTimes />, // times/close variant
};

type TIcon = {
	code?: IconCode;
	className?: string;
	type?: 'solid' | 'duotone' | string;
	size?: number;
	children?: ReactNode;
};

const Icon = ({ code, className, type, size, children }: TIcon) => {
	// If children provided (direct ReactNode), render it
	if (children) {
		return <span className={className} style={{ fontSize: size }}>{children}</span>;
	}

	// If code provided, look it up in the map
	if (code) {
		const icon = iconMap[code];
		if (icon) {
			return <span className={className} style={{ fontSize: size }}>{icon}</span>;
		}
		// Fallback for unmapped codes
		console.warn(`Icon code '${code}' not mapped to react-icons`);
		return <span className={className} style={{ fontSize: size }}>�</span>;
	}

	return null;
};

export default Icon;
