import Link from 'next/link';
import { ThemeSelect } from 'xtreme-ui';

import './navbar.scss';

export default function Navbar({ menuOpen, setMenuOpen }: TNavBarProps) {
	return (
		<div className='aboutNavbar' id='about-navBar'>
			<Link href="/" className='logo'>YURT</Link>
			<div className={`menu ${menuOpen ? 'open' : ''}`}>
				<div className='icon round' onClick={() => setMenuOpen(!menuOpen)}>
					<span className='line1' />
					<span className='line2' />
				</div>
				<div className='container'>
					<Link href="/" className='item'>
						<p>Home</p>
					</Link>
					<Link href="/admin/login" className='item'>
						<p>Admin Login</p>
					</Link>
				</div>
				<ThemeSelect size='mini' withSwatch withScheme />
			</div>
		</div>
	);
}

type TNavBarProps = {
	menuOpen: boolean;
	setMenuOpen: (menuOpen: boolean) => void;
};
