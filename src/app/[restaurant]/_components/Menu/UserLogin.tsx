import { useState, useEffect } from 'react';

import { usePathname, useSearchParams } from 'next/navigation';
import { signIn } from 'next-auth/react';
import { toast } from 'react-toastify';

import Textfield from '#components/base/Textfield';
import { useRestaurant } from '#components/context/useContext';

import './userLogin.scss';

const mobileNumberPattern = /^(\+91[-\s]?)?[6-9]\d{9}$/;
const UserLogin = ({ setOpen }: UserLoginProps) => {
	const pathname = usePathname();
	const params = useSearchParams();
	const { selectedAddress } = useRestaurant();
	const [page, setPage] = useState('phone');
	const [buttonLabel, setButtonLabel] = useState('Next');
	const [busy, setBusy] = useState(false);

	const [dialCode] = useState('7');
	const [phone, setPhone] = useState('');

	const [fname, setFName] = useState('');
	const [lname, setLName] = useState('');
	const [heading, setHeading] = useState(['Let\'s', ' start ordering']);

	const phoneNumber = `+${dialCode}${phone}`;
	const onNext = async () => {
		if (page === 'phone') {
			if (phone.length < 10) {
				return toast.error('Please enter a valid phone number');
			}

			setBusy(true);
			setTimeout(() => {
				setBusy(false);
				setPage('signOTP');
			}, 400);
		}

		else if (page === 'signOTP' || page === 'loginOTP') {
			if (!selectedAddress) return toast.error('Please select a pickup address');

			setBusy(true);

			const res = await signIn('customer', {
				redirect: false,
				restaurant: pathname.replaceAll('/', ''),
				phone: phoneNumber,
				fname,
				lname,
				address: selectedAddress,
				callbackUrl: `${window.location.origin}`,
			});

			if (res?.error) {
				toast.error(res?.error);
			}
			setOpen(false);
			setBusy(false);
		}
	};

	useEffect(() => {
		if (page === 'phone') {
			setHeading(['Let\'s', ' start ordering']);
			setButtonLabel('Next');
		} else if (page === 'signOTP') {
			setHeading(['Glad to', ' see you here']);
			setButtonLabel('Order');
		} else if (page === 'loginOTP') {
			setHeading(['Welcome', ' back User']);
			setButtonLabel('Log In');
		}
	}, [page]);

	return (
		<div className={`userLogin ${page}`}>
			<div className='header'>
				<span className='heading'><span>{heading[0]}</span>{heading[1]}</span>
			</div>
			<div className='content'>
				<div className='phoneInputWrapper'>
					<div className='countryCode'>
						<span className='flag'>🇰🇿</span>
						<span className='code'>+7</span>
					</div>
					<Textfield
						className='phone'
						type='tel'
						autoComplete='tel-local'
						placeholder='Phone number'
						value={phone}
						onEnterKey={onNext}
						onChange={(e) => setPhone(e.target.value.replace(/\D/g, ''))}
					/>
				</div>
				<div className='otpContainer'>
					<Textfield
						className='fName'
						placeholder='First Name'
						autoComplete='given-name'
						value={fname}
						onChange={(e) => setFName(e.target.value)}
					/>
					<Textfield
						className='lName'
						placeholder='Last Name'
						autoComplete='family-name'
						onEnterKey={onNext}
						value={lname}
						onChange={(e) => setLName(e.target.value)}
					/>
					{/* <Textfield
						className='otp'
						placeholder='Enter Your otp'
						autoComplete='one-time-code'
						value={otp}
						onChange={(e) => setOtp(e.target.value)}
					/> */}
				</div>
			</div>
			<div className='footer'>
				<button className='xButton loginButton' disabled={busy} onClick={onNext}>
					{busy ? 'Loading...' : buttonLabel}
				</button>
			</div>
		</div>
	);
};

export default UserLogin;

type UserLoginProps = {
	setOpen: (open: boolean) => void
}
